using System;
using System.Collections.Generic;
using System.Linq;
using Common.Logging;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Makaretu.Dns;
using System.Net.Sockets;

namespace Peds
{
    /// <summary>
    ///   A forwarding DNS server.
    /// </summary>
    /// <remarks>
    ///   Sends a DNS request to a recursive DNS server and returns the response.
    /// </remarks>
    class UdpServer : IDisposable
    {
        static ILog log = LogManager.GetLogger(typeof(UdpServer));

        /// <summary>
        ///   Something that can resolve a DNS query.
        /// </summary>
        /// <value>
        ///   A client to a recursive DNS Server.
        /// </value>
        public IDnsClient Resolver { get; set; }

        /// <summary>
        ///   The port to listen to.
        /// </summary>
        /// <value>
        ///   Defaults to 53.
        /// </value>
        public int Port { get; set; } = 53;

        public async Task StartAsync()
        {
            foreach (var address in Addresses)
            {
                var endPoint = new IPEndPoint(address, Port);
                var listener = new UdpClient(endPoint);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                Task.Run(() => ReadRequests(listener));
#pragma warning restore CS4014
            }
        }

        /// <summary>
        ///   The addresses of the server.
        /// </summary>
        /// <value>
        ///   Defaults to <see cref="IPAddress.Loopback"/>.
        /// </value>
        public IEnumerable<IPAddress> Addresses { get; set; } = new[] 
        {
            IPAddress.Loopback
        };

        Task<Message> ProcessAsync(Message message)
        {
            return Resolver.QueryAsync(message);
        }

        public void Dispose()
        {
            // TODO
        }

        async Task ReadRequests(UdpClient listener)
        {
            if (log.IsDebugEnabled)
                log.Debug("Starting reader thread");

            while (true)
            {
                try
                {
                    var result = await listener.ReceiveAsync();
                    var query = (Message)new Message().Read(result.Buffer);
                    var response = await ProcessAsync(query);
                    await listener.SendAsync(response.ToByteArray(), 0, result.RemoteEndPoint);
                }
                catch (Exception e)
                {
                    log.Error(e);
                }
            }

            if (log.IsDebugEnabled)
                log.Debug($"Stopping reader thread");

        }

    }
}
