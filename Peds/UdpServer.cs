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

        public void Start()
        {
            foreach (var address in Addresses)
            {
                var endPoint = new IPEndPoint(address, Port);
                var listener = new UdpClient(endPoint);
                ReadRequests(listener);
            }
        }

        /// <summary>
        ///   The addresses of the server.
        /// </summary>
        /// <value>
        ///   Defaults to <see cref="IPAddress.IPv6Loopback"/> and <see cref="IPAddress.Loopback"/>.
        /// </value>
        public IEnumerable<IPAddress> Addresses { get; set; } = new[] 
        {
            IPAddress.IPv6Loopback,
            IPAddress.Loopback
        };

        public void Dispose()
        {
            // TODO
        }

        async void ReadRequests(UdpClient listener)
        {
            while (true)
            {
                try
                {
                    var request = await listener.ReceiveAsync();
                    log.Debug("got request");
                    Process(request, listener);
                }
                catch (Exception e)
                {
                    log.Error(e);
                }
            }
        }

        async void Process(UdpReceiveResult request, UdpClient listener)
        {
            try
            {
                var query = (Message)new Message().Read(request.Buffer);
                var response = await Resolver.QueryAsync(query);
                var responseBytes = response.ToByteArray();
                await listener.SendAsync(responseBytes, responseBytes.Length, request.RemoteEndPoint);
            }
            catch (Exception e)
            {
                log.Error(e);
            }
        }

    }
}
