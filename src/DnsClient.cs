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
    ///   Sends and receives DNS queries and answers.
    /// </remarks>
    public class DnsClient
    {
        static ILog log = LogManager.GetLogger(typeof(DnsClient));

        const int DnsPort = 53;

        /// <summary>
        ///   Time to wait for a DNS UDP response.
        /// </summary>
        /// <value>
        ///   The default is 4 seconds.
        /// </value>
        public static TimeSpan TimeoutUdp { get; set; } = TimeSpan.FromSeconds(4);

        /// <summary>
        ///   Time to wait for a DNS TCP response.
        /// </summary>
        /// <value>
        ///   The default is 4 seconds.
        /// </value>
        public static TimeSpan TimeoutTcp { get; set; } = TimeSpan.FromSeconds(4);

        static IEnumerable<IPAddress> servers;

        /// <summary>
        ///   The DNS servers to communication with.
        /// </summary>
        /// <value>
        ///   A sequence of IP addresses.  When <b>null</b> <see cref="GetServers"/>
        ///   is used. The default is <b>null</b>.
        /// </value>
        public static IEnumerable<IPAddress> Servers
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
        ///   Get the DNS servers.
        /// </summary>
        /// <returns>
        ///   A sequence of IP addresses for the DNS servers.
        /// </returns>
        public static IEnumerable<IPAddress> GetServers()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(nic => nic.GetIPProperties().DnsAddresses);
        }

        /// <summary>
        ///   Send a DNS query.
        /// </summary>
        /// <param name="request">
        ///   A <see cref="Message"/> containing a <see cref="Question"/>.
        /// </param>
        /// <param name="cancel">
        ///   Is used to stop the task.  When cancelled, the <see cref="TaskCanceledException"/> is raised.
        /// </param>
        /// <returns>
        ///   A task that represents the asynchronous get operation. The task's value
        ///   contains the response <see cref="Message"/>.
        /// </returns>
        /// <exception cref="IOException">
        ///   When the DNS server returns error status or no response.
        /// </exception>
        /// <remarks>
        ///   The <paramref name="request"/> is sent with UDP.  If no response is
        ///   received (or is truncated) in <see cref="TimeoutUdp"/>, then it is resent via TCP.
        /// </remarks>
        public static async Task<Message> QueryAsync(
            Message request,
            CancellationToken cancel = default(CancellationToken))
        {
            var msg = request.ToByteArray();
            Message response = null;

            // Try UDP first.
            var cs = new CancellationTokenSource(TimeoutUdp);
            try
            {
                response = await QueryUdpAsync(msg, cs.Token);
                // If truncated response, then use TCP.
                if (response != null && response.TC)
                {
                    response = null; 
                }
            }
            catch (TaskCanceledException)
            {
                // Timeout, will retry with TCP 
            }

            // If no response, then try TCP
            if (response == null)
            {
                cs = new CancellationTokenSource(TimeoutTcp);
                try
                {
                    response = await QueryTcpAsync(msg, cs.Token);
                }
                catch (TaskCanceledException)
                {
                    // Timeout
                }
            }

            // Check the response.
            if (response == null)
                throw new IOException("No response from DNS servers.");
            if (response.Status != MessageStatus.NoError)
                throw new IOException($"DNS error '{response.Status}'.");

            return response;
        }

        static async Task<Message> QueryUdpAsync(
            byte[] request,
            CancellationToken cancel)
        {
            foreach (var server in Servers/*.Where(s => s.AddressFamily == AddressFamily.InterNetwork)*/)
            {
                var endPoint = new IPEndPoint(server, DnsPort);
                log.Debug("UDP to " + endPoint.ToString());

                using (var client = new UdpClient(server.AddressFamily))
                {
                    try
                    {
                        await client.SendAsync(request, request.Length, endPoint);
                        var result = await client
                            .ReceiveAsync()
                            .WaitAsync(cancel);
                        var response = (Message)(new Message().Read(result.Buffer));
                        return response;
                    }
                    catch (Exception e)
                    {
                        log.Error(e.Message);
                    }
                }
            }
            return null;

            using (var client = new UdpClient())
            {
                var servers = Servers
                    .Where(s => s.AddressFamily == AddressFamily.InterNetwork);  // TODO IPv6
                var server = new IPEndPoint(servers.First(), DnsPort);
                await client.SendAsync(request, request.Length, server);

                var result = await client
                    .ReceiveAsync()
                    .WaitAsync(cancel);
                var response = (Message)(new Message().Read(result.Buffer));
                return response; 
            }
        }

        static async Task<Message> QueryTcpAsync(
            byte[] request,
            CancellationToken cancel)
        {
            using (var client = new TcpClient())
            {
                await client
                    .ConnectAsync(Servers.ToArray(), DnsPort)
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
                        throw new Exception($"No response from DNS server.");
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
