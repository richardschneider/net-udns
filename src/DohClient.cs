using Common.Logging;
using Nito.AsyncEx; 
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Makaretu.Dns
{
    /// <summary>
    ///   Client to a DNS server over HTTPS.
    /// </summary>
    /// <remarks>
    ///   DNS over HTTPS (DoH) is an experimental protocol for performing remote 
    ///   Domain Name System (DNS) resolution via the HTTPS protocol. The goal
    ///   is to increase user privacy and security by preventing eavesdropping and 
    ///   manipulation of DNS data by man-in-the-middle attacks.
    /// </remarks>
    /// <seealso href="https://en.wikipedia.org/wiki/DNS_over_HTTPS"/>
    public class DohClient
    {
        /// <summary>
        ///   The MIME type for a DNS message encoded in UPD wire format.
        /// </summary>
        public const string DnsWireFormat = "application/dns-udpwireformat";

        /// <summary>
        ///   The MIME type for a DNS message encoded in JSON.
        /// </summary>
        public const string DnsJsonFormat = "application/dns-json";

        static ILog log = LogManager.GetLogger(typeof(DohClient));

        /// <summary>
        ///   Time to wait for a DNS response.
        /// </summary>
        /// <value>
        ///   The default is 4 seconds.
        /// </value>
        public static TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(4);

        /// <summary>
        ///   The DNS server to communication with.
        /// </summary>
        /// <value>
        ///   Defaults to "https://cloudflare-dns.com/dns-query".
        /// </value>
        public static string ServerUrl { get; set; } = "https://cloudflare-dns.com/dns-query";

        static HttpClient httpClient;
        static object httpClientLock = new object();

        /// <summary>
        ///   The client that sends HTTP requests and receives HTTP responses.
        /// </summary>
        /// <remarks>
        ///   It is best practice to use only one <see cref="HttpClient"/> in an
        ///   application.
        /// </remarks>
        public static HttpClient HttpClient
        {
            get
            {
                if (httpClient == null)
                {
                    lock (httpClientLock)
                    {
                        httpClient = new HttpClient();
                    }
                }
                return httpClient;
            }
            set
            {
                httpClient = value;
            }
        }

        /// <summary>
        ///   Get the IP addresses for the specified name.
        /// </summary>
        /// <param name="name">
        ///   A domain name.
        /// </param>
        /// <param name="cancel">
        ///   Is used to stop the task.  When cancelled, the <see cref="TaskCanceledException"/> is raised.
        /// </param>
        /// <returns>
        ///   A task that represents the asynchronous operation. The task's value
        ///   contains the <see cref="IPAddress"/> sequence for the <paramref name="name"/>.
        /// </returns>
        public static async Task<IEnumerable<IPAddress>> ResolveAsync(
            string name,
            CancellationToken cancel = default(CancellationToken))
        {
            var a = QueryAsync(name, DnsType.A, cancel);
            var aaaa = QueryAsync(name, DnsType.AAAA, cancel);
            var responses = await Task.WhenAll(a, aaaa);
            return responses
                .SelectMany(m => m.Answers)
                .Where(rr => rr.Type == DnsType.A || rr.Type == DnsType.AAAA)
                .Select(rr => rr.Type == DnsType.A
                    ? ((ARecord)rr).Address
                    : ((AAAARecord)rr).Address);
        }

        /// <summary>
        ///   Send a DNS query with the specified name and resource record type.
        /// </summary>
        /// <param name="name">
        ///   A domain name.
        /// </param>
        /// <param name="rtype">
        ///   A resource record type.
        /// </param>
        /// <param name="cancel">
        ///   Is used to stop the task.  When cancelled, the <see cref="TaskCanceledException"/> is raised.
        /// </param>
        /// <returns>
        ///   A task that represents the asynchronous operation. The task's value
        ///   contains the response <see cref="Message"/>.
        /// </returns>
        /// <remarks>
        ///   Creates a query <see cref="Message"/> and then calls <see cref="QueryAsync(Message, CancellationToken)"/>.
        /// </remarks>
        public static Task<Message> QueryAsync(
            string name,
            DnsType rtype,
            CancellationToken cancel = default(CancellationToken))
        {
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = name, Type = rtype });

            return QueryAsync(query, cancel);
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
        ///   received (or is truncated) in <see cref="Timeout"/>, then it is resent via TCP.
        ///   <para>
        ///   Some home routers have issues with IPv6, so IPv4 servers are tried first.
        ///   </para>
        /// </remarks>
        public static async Task<Message> QueryAsync(
            Message request,
            CancellationToken cancel = default(CancellationToken))
        {
            if (log.IsDebugEnabled)
            {
                var names = request.Questions
                    .Select(q => q.Name + " " + q.Type.ToString())
                    .Aggregate((current, next) => current + ", " + next);
                log.Debug($"query #{request.Id} for '{names}'");
            }

            // Cancel the request when either the timeout is reached or the
            // task is cancelled by the caller.
            var cts = CancellationTokenSource
                .CreateLinkedTokenSource(cancel);
            cts.CancelAfter(Timeout);

            // Post the request.
            var content = new ByteArrayContent(request.ToByteArray());
            content.Headers.ContentType = new MediaTypeHeaderValue(DnsWireFormat);
            var httpResponse = await HttpClient.PostAsync(ServerUrl, content, cts.Token);

            // Check the HTTP response.
            httpResponse.EnsureSuccessStatusCode();
            var contentType = httpResponse.Content.Headers.ContentType.MediaType;
            if (DnsWireFormat != contentType)
                throw new HttpRequestException($"Expected content-type '{DnsWireFormat}' not '{contentType}'.");

            // Check the DNS response.
            var body = await httpResponse.Content.ReadAsStreamAsync();
            var dnsResponse = (Message)new Message().Read(body);
            if (dnsResponse.Status != MessageStatus.NoError) {
                log.Warn($"DNS error '{dnsResponse.Status}'.");
                throw new IOException($"DNS error '{dnsResponse.Status}'.");
            }

            if (log.IsDebugEnabled)
                log.Debug($"Got response #{dnsResponse.Id}");
            return dnsResponse;
        }


    }
}
