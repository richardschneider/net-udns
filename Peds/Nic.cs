using Common.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Peds
{
    /// <summary>
    ///   Controls the network interfaces.
    /// </summary>
    /// <remarks>
    ///   <b>Dispose</b> restores all networks to thier orignal values.
    ///   <note>
    ///   Requires elevated privileges.
    ///   </note>
    /// </remarks>
    class Nic : IDisposable
    {
        static ILog log = LogManager.GetLogger(typeof(Nic));

        Dictionary<NetworkInterface, IPAddressCollection> originalDnsServers = new Dictionary<NetworkInterface, IPAddressCollection>();

        /// <summary>
        ///   Set the DNS server addresses for all network interfaces.
        /// </summary>
        /// <param name="server">
        ///   The sequence of <see cref="IPAddress"/> for the DNS server.
        /// </param>
        public void SetDnsServer(IEnumerable<IPAddress> addresses)
        {
            var netsh = new NetShell();
            var nics = NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Where(nic => 
                    nic.Supports(NetworkInterfaceComponent.IPv4) ||
                    nic.Supports(NetworkInterfaceComponent.IPv6));
            foreach (var nic in nics)
            {
                var original = nic.GetIPProperties().DnsAddresses;
                originalDnsServers[nic] = original;
                netsh.SetDnsServers(nic, addresses);
            }
            netsh.Run();
        }

        public void Dispose()
        {
            var netsh = new NetShell();
            foreach (var x in originalDnsServers)
            {
                netsh.SetDnsServers(x.Key, x.Value);
            }
            netsh.Run();
        }

    }
}
