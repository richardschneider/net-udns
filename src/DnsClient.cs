using Common.Logging;
using Nito.AsyncEx; 
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Makaretu.Dns
{
    /// <summary>
    ///   Client to a unicast DNS server.
    /// </summary>
    /// <remarks>
    ///   Sends and receives DNS queries and answers to unicast DNS servers.
    /// </remarks>
    public class DnsClient : DnsClientBase
    {
        static ILog log = LogManager.GetLogger(typeof(DnsClient));

        const int DnsPort = 53;

        /// <summary>
        ///   Time to wait for a DNS UDP response.
        /// </summary>
        /// <value>
        ///   The default is 4 seconds.
        /// </value>
        public TimeSpan TimeoutUdp { get; set; } = TimeSpan.FromSeconds(4);

        /// <summary>
        ///   Time to wait for a DNS TCP response.
        /// </summary>
        /// <value>
        ///   The default is 4 seconds.
        /// </value>
        public TimeSpan TimeoutTcp { get; set; } = TimeSpan.FromSeconds(4);

        IEnumerable<IPAddress> servers;

        /// <summary>
        ///   The DNS servers to communication with.
        /// </summary>
        /// <value>
        ///   A sequence of IP addresses.  When <b>null</b> <see cref="GetServers"/>
        ///   is used. The default is <b>null</b>.
        /// </value>
        public IEnumerable<IPAddress> Servers
        {
            get
            {
                return servers ?? GetServers();
            }
            set
            {
                servers = value;
            }
        }

        /// <summary>
        ///   Get the DNS servers that can be communicated with.
        /// </summary>
        /// <returns>
        ///   A sequence of IP addresses for the DNS servers.
        /// </returns>
        /// <remarks>
        ///   Only servers with an <see cref="AddressFamily"/> supported by
        ///   the OS is returned.
        /// </remarks>
        public IEnumerable<IPAddress> AvailableServers()
        {
            return Servers
                .Where(a =>
                    (Socket.OSSupportsIPv4 && a.AddressFamily == AddressFamily.InterNetwork) ||
                    (Socket.OSSupportsIPv6 && a.AddressFamily == AddressFamily.InterNetworkV6));
        }

        /// <summary>
        ///   Get the DNS servers.
        /// </summary>
        /// <returns>
        ///   A sequence of IP addresses for the DNS servers.
        /// </returns>
        public IEnumerable<IPAddress> GetServers()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(nic => nic.GetIPProperties().DnsAddresses);
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
        ///   The <paramref name="request"/> is sent with UDP.  If no response is
        ///   received (or is truncated) in <see cref="TimeoutUdp"/>, then it is resent via TCP.
        ///   <para>
        ///   Some home routers have issues with IPv6, so IPv4 servers are tried first.
        ///   </para>
        /// </remarks>
        public override async Task<Message> QueryAsync(
            Message request,
            CancellationToken cancel = default(CancellationToken))
        {
            var servers = AvailableServers()
                .OrderBy(a => a.AddressFamily)
                .ToArray();
            if (servers.Length == 0)
                throw new Exception("No DNS servers are available.");

            if (log.IsDebugEnabled)
            {
                var names = request.Questions
                    .Select(q => q.Name + " " + q.Type.ToString())
                    .Aggregate((current, next) => current + ", " + next);
                log.Debug($"query #{request.Id} for '{names}'");
            }
            var msg = request.ToByteArray();
            Message response = null;

            foreach (var server in servers)
            {
                response = await QueryAsync(msg, server, cancel);
                if (response != null)
                    break;
            }

            // Check the response.
            if (response == null)
            {
                log.Warn("No response from DNS servers.");
                throw new IOException("No response from DNS servers.");
            }
            if (ThrowResponseError)
            {
                if (response.Status != MessageStatus.NoError)
                {
                    log.Warn($"DNS error '{response.Status}'.");
                    throw new IOException($"DNS error '{response.Status}'.");
                }
            }

            if (log.IsDebugEnabled)
                log.Debug($"Got response #{response.Id}");
            if (log.IsTraceEnabled)
                log.Trace(response);
            return response;
        }

        async Task<Message> QueryAsync(byte[] request, IPAddress server, CancellationToken cancel)
        {
            // Try UDP first.
            var cts = CancellationTokenSource.CreateLinkedTokenSource(
                cancel,
                new CancellationTokenSource(TimeoutUdp).Token);
            try
            {
                var response = await QueryUdpAsync(request, server, cts.Token);
                // If truncated response, then use TCP.
                if (response != null && !response.TC)
                {
                    return response;
                }
            }
            catch (SocketException e)
            {
                // Cannot connect, try another server.
                log.Warn(e.Message);
                return null;
            }
            catch (TaskCanceledException e)
            {
                // Timeout, will retry with TCP 
                log.Warn(e.Message);
            }

            // If no response, then try TCP
            cts = CancellationTokenSource.CreateLinkedTokenSource(
                cancel,
                new CancellationTokenSource(TimeoutTcp).Token);
            try
            {
                return await QueryTcpAsync(request, server, cts.Token);
            }
            catch (Exception e)
            {
                log.Warn(e.Message);
                return null;
            }
        }

        async Task<Message> QueryUdpAsync(
            byte[] request,
            IPAddress server,
            CancellationToken cancel)
        {
            var endPoint = new IPEndPoint(server, DnsPort);
            log.Debug("UDP to " + endPoint.ToString());

            using (var client = new UdpClient(server.AddressFamily))
            {
                await client.SendAsync(request, request.Length, endPoint);
                var result = await client
                    .ReceiveAsync()
                    .WaitAsync(cancel);
                var response = (Message)(new Message().Read(result.Buffer));
                return response;
            }
        }

        async Task<Message> QueryTcpAsync(
            byte[] request,
            IPAddress server,
            CancellationToken cancel)
        {
            log.Debug("TCP to " + server.ToString());

            using (var client = new TcpClient(server.AddressFamily))
            {
                await client
                    .ConnectAsync(server, DnsPort)
                    .WaitAsync(cancel);
                using (var stream = client.GetStream())
                {
                    // The message is prefixed with a two byte length field which gives 
                    // the message length, excluding the two byte length field.
                    byte[] length = BitConverter.GetBytes((ushort)request.Length);
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(length);
                    }
                    await stream.WriteAsync(length, 0, length.Length, cancel);
                    await stream.WriteAsync(request, 0, request.Length, cancel);
                    await stream.FlushAsync();

                    // Read response length
                    var buffer = new byte[2];
                    var n = await stream
                        .ReadAsync(buffer, 0, buffer.Length)
                        .WaitAsync(cancel);
                    if (n == 0)
                        return null;
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(buffer);
                    }
                    var responseLength = BitConverter.ToUInt16(buffer, 0);

                    // Read response message
                    buffer = new byte[responseLength];
                    n = await stream.ReadAsync(buffer, 0, buffer.Length, cancel);
                    var response = (Message)(new Message().Read(buffer, 0, n));
                    return response;
                }
            }
        }

    }
}
