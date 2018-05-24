using Common.Logging;
using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Makaretu.Dns
{
    /// <summary>
    ///   Client to a DNS server over TLS.
    /// </summary>
    /// <remarks>
    ///   DNS over TLS is a security protocol for encrypting and wrapping 
    ///   DNS queries and answers via the Transport Layer Security (TLS) protocol. The goal 
    ///   is to increase user privacy and security by preventing eavesdropping and 
    ///   manipulation of DNS data via man-in-the-middle attacks.
    ///   <para>
    ///   All queries are padded to the closest multiple of <see cref="BlockLength"/> octets.
    ///   </para>
    /// </remarks>
    /// <seealso href="https://tools.ietf.org/html/rfc7858"/>
    /// <seealso href="https://tools.ietf.org/html/rfc8310"/>
    public class DotClient : DnsClientBase
    {
        SslStream dnsServer;
        readonly AsyncLock dnsServerLock = new AsyncLock();

        /// <summary>
        ///   The default port of a DOT server.
        /// </summary>
        public const int DefaultPort = 853;

        /// <summary>
        ///   Known servers that support DNS over TLS.
        /// </summary>
        /// <value>
        ///   Sequence of known servers.
        /// </value>
        /// <remarks>
        ///   This is the default list that <see cref="Servers"/> uses.
        /// </remarks>
        public static DotEndPoint[] PublicServers = new[]
        {
            new DotEndPoint
            {
                Hostname = "cloudflare-dns.com",
                Address = IPAddress.Parse("1.1.1.1")
            },
            new DotEndPoint
            {
                Hostname = "cloudflare-dns.com",
                Address = IPAddress.Parse("1.0.0.1")
            },
            new DotEndPoint
            {
                Hostname = "cloudflare-dns.com",
                Address = IPAddress.Parse("2606:4700:4700::1111")
            },
            new DotEndPoint
            {
                Hostname = "cloudflare-dns.com",
                Address = IPAddress.Parse("2606:4700:4700::1001")
            },
            new DotEndPoint
            {
                Hostname = "securedns.eu",
                Pins = new[] { "h3mufC43MEqRD6uE4lz6gAgULZ5/riqH/E+U+jE3H8g=" },
                Address = IPAddress.Parse("146.185.167.43")
            },
            new DotEndPoint
            {
                Hostname = "securedns.eu",
                Pins = new[] { "h3mufC43MEqRD6uE4lz6gAgULZ5/riqH/E+U+jE3H8g=" },
                Address = IPAddress.Parse("2a03:b0c0:0:1010::e9a:3001")
            },
            new DotEndPoint
            {
                Hostname = "dns.quad9.net",
                Address = IPAddress.Parse("9.9.9.9")
            },
        };

        static ILog log = LogManager.GetLogger(typeof(DotClient));

        ushort nextQueryId = (ushort)new Random().Next((int)ushort.MaxValue + 1);

        /// <summary>
        ///   The number of octets for padding.
        /// </summary>
        /// <value>
        ///   Defaults to 128.
        /// </value>
        /// <remarks>
        ///   All queries are padded to the closest multiple of <see cref="BlockLength"/> octets.
        /// </remarks>
        /// <seealso href="https://tools.ietf.org/html/draft-ietf-dprive-padding-policy-05#section-4.1"/>
        public int BlockLength { get; set; } = 128;

        /// <summary>
        ///   Time to wait for a DNS response.
        /// </summary>
        /// <value>
        ///   The default is 4 seconds.
        /// </value>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(4);

        /// <summary>
        ///   The DNS over TLS servers to communication with.
        /// </summary>
        /// <value>
        ///   A sequence of DOT endpoints.  The default is the <see cref="PublicServers"/>.
        /// </value>
        public IEnumerable<DotEndPoint> Servers { get; set; } = PublicServers;

        /// <summary>
        ///   Outstanding requests.
        /// </summary>
        /// <value>
        ///   Key is the request's <see cref="Message.Id"/>.
        /// </value>
        /// <remarks>
        ///   Contains the requests that are waiting for a response.
        /// </remarks>
        ConcurrentDictionary<ushort, TaskCompletionSource<Message>> OutstandingRequests = new ConcurrentDictionary<ushort, TaskCompletionSource<Message>>();

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (dnsServer != null)
                {
                    dnsServer.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        /// <summary>
        ///   Send a DNS query with the specified message.
        /// </summary>
        /// <param name="request">
        ///   A <see cref="Message"/> containing a <see cref="Question"/>.
        /// </param>
        /// <param name="cancel">
        ///   Is used to stop the task.  When cancelled, the <see cref="TaskCanceledException"/> is raised.
        /// </param>
        /// <returns>
        ///   A task that represents the asynchronous operation. The task's value
        ///   contains the response <see cref="Message"/>.
        /// </returns>
        /// <exception cref="IOException">
        ///   When the DNS server returns error status or no response.
        /// </exception>
        /// <remarks>
        ///   Sends the <paramref name="request"/> and waits for
        ///   the matching response.
        /// </remarks>
        public override async Task<Message> QueryAsync(
            Message request,
            CancellationToken cancel = default(CancellationToken))
        {
            // Find a server.
            var server = await GetDnsServerAsync();
            if (server == null)
                throw new Exception("No DNS over TLS server can be found.");

            // Build the TCP request.
            var tcpRequest = BuildRequest(request);
            if (log.IsDebugEnabled)
            {
                var names = request.Questions
                    .Select(q => q.Name + " " + q.Type.ToString())
                    .Aggregate((current, next) => current + ", " + next);
                log.Debug($"query #{request.Id} for '{names}'");
            }

            // Cancel the request when either the timeout is reached or the
            // task is cancelled by the caller.
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            cts.CancelAfter(Timeout);
            var tcs = new TaskCompletionSource<Message>();
            OutstandingRequests[request.Id] = tcs;

            Message dnsResponse;
            try
            {
                // Only one writer at a time.
                using (await dnsServerLock.LockAsync())
                {
                    await server.WriteAsync(tcpRequest, 0, tcpRequest.Length, cts.Token);
                }
                dnsResponse = await tcs.Task.WaitAsync(cts.Token);
            }
            finally
            {
                OutstandingRequests.TryRemove(request.Id, out var _);
            }

            // Checks that response is valid.
            if (!dnsResponse.IsResponse)
                throw new FormatException("DNS response is not a response.");
            if (dnsResponse.TC)
                throw new FormatException("DNS response should not be truncated.");
            // TODO: Check that answer matches.

            if (dnsResponse.Status != MessageStatus.NoError)
            {
                log.Warn($"DNS error '{dnsResponse.Status}'.");
                throw new IOException($"DNS error '{dnsResponse.Status}'.");
            }

            return dnsResponse;
        }

        byte[] BuildRequest(Message request)
        {
            request.Id = nextQueryId++;

            // Add an OPT if not already present.
            var opt = request.AdditionalRecords.OfType<OPTRecord>().FirstOrDefault();
            if (opt == null)
            {
                opt = new OPTRecord();
                request.AdditionalRecords.Add(opt);
            }

            // Always use padding.
            if (!opt.Options.Any(o => o.Type == EdnsOptionType.Padding))
            {
                var paddingOption = new EdnsPaddingOption();
                opt.Options.Add(paddingOption);
                var need = BlockLength - (request.ToByteArray().Length % BlockLength);
                if (need > 0)
                    paddingOption.Padding = new byte[need];
            };

            var udpRequest = request.ToByteArray();
            byte[] length = BitConverter.GetBytes((ushort)udpRequest.Length);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(length);
            }
            var tcpRequest = new byte[length.Length + udpRequest.Length];
            length.CopyTo(tcpRequest, 0);
            udpRequest.CopyTo(tcpRequest, length.Length);

            return tcpRequest;
        }

        /// <summary>
        ///   Get the stream to a DNS server.
        /// </summary>
        /// <returns></returns>
        public async Task<Stream> GetDnsServerAsync()
        {
            // Is current server still good to go?
            if (dnsServer != null && dnsServer.CanRead && dnsServer.CanWrite)
                return dnsServer;

            using (await dnsServerLock.LockAsync())
            {
                if (dnsServer != null && dnsServer.CanRead && dnsServer.CanWrite)
                    return dnsServer;
                if (dnsServer != null)
                    dnsServer.Dispose();

                var servers =  Servers.Where(s =>
                    (Socket.OSSupportsIPv4 && s.Address.AddressFamily == AddressFamily.InterNetwork) ||
                    (Socket.OSSupportsIPv6 && s.Address.AddressFamily == AddressFamily.InterNetworkV6));
                foreach (var endPoint in servers)
                {
                    try
                    {
                        var socket = new Socket(endPoint.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                        await socket.ConnectAsync(endPoint.Address, endPoint.Port);
                        var stream = new NetworkStream(socket, ownsSocket: true);

                        dnsServer = new SslStream(
                            stream,
                            false, // leave inner stream open
                            (sender, certificate, chain, errors) =>
                            {
                                return ValidateServerCertificate(sender, certificate, chain, errors, endPoint.Pins);
                            },
                            null,
                            EncryptionPolicy.RequireEncryption);
                        await dnsServer.AuthenticateAsClientAsync(endPoint.Hostname, null, SslProtocols.Tls12, true);

                        if (log.IsDebugEnabled)
                            log.Debug($"using dns server '{endPoint.Hostname}' {endPoint.Address}.");

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        Task.Run(() => ReadResponses(dnsServer));
#pragma warning restore CS4014

                        return dnsServer;
                    }
                    catch (Exception e)
                    {
                        //Console.WriteLine($"Connection to {endPoint.Address} failed.");
                        //for (var ex = e; ex != null; ex = ex.InnerException)
                        //    Console.WriteLine(ex.Message);
                        log.Warn($"Connection to {endPoint.Address} failed.", e);
                    }
                }
            }

            return null;
        }

        static bool ValidateServerCertificate(
          object sender,
          X509Certificate certificate,
          X509Chain chain,
          SslPolicyErrors sslPolicyErrors,
          string[] pins)
        {
            if (sslPolicyErrors != SslPolicyErrors.None)
                return false;

            // Verify that the certificate's SPKI matches one of the PINs.
            if (pins == null || pins.Length == 0)
                return true;
            
            // see https://github.com/richardschneider/net-udns/issues/5
#if false
            string spki = ""; // TODO: base-64 of certificates SPKI
            return pins.Any(pin => pin == spki);
#else
            return true;
#endif

        }

        void ReadResponses(Stream stream)
        {
            if (log.IsDebugEnabled)
                log.Debug($"Starting reader thread");

            var reader = new DnsReader(stream);
            while (stream.CanRead)
            {
                try
                {
                    var length = reader.ReadUInt16();
                    // TODO: Check MinLength
                    if (length > Message.MaxLength)
                       throw new InvalidDataException("DNS response exceeded max length.");
                    // TODO: Should work, but doesn't
                    //var response = (Message)new Message().Read(reader);
                    var response = (Message)new Message().Read(reader.ReadBytes(length));

                    // Find matching request.
                    if (log.IsDebugEnabled)
                        log.Debug($"Got response #{response.Id}");
                    if (!OutstandingRequests.TryGetValue(response.Id, out var task))
                    {
                        log.Warn("DNS response is missing a matching request ID.");
                        continue;
                    }

                    // Continue the request.
                    task.SetResult(response);
                }
                catch (Exception e)
                {
                    if (stream.CanRead)
                    {
                        log.Error(e);
                    }
                    stream.Dispose();
                }
            }

            if (log.IsDebugEnabled)
                log.Debug($"Stopping reader thread");
        }
    }


}
