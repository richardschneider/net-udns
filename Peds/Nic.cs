using Common.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Sockets;

namespace Peds
{
    /// <summary>
    ///   Controls the network interfaces.
    /// </summary>
    /// <remarks>
    ///   <b>Dispose</b> restores all networks to thier orignal values.
    /// </remarks>
    class Nic : IDisposable
    {
        static ILog log = LogManager.GetLogger(typeof(Nic));

        Dictionary<string, string[]> originalDnsServers = new Dictionary<string, string[]>();

        public void Dispose()
        {
            var mc = new ManagementClass("Win32_NetworkAdapterConfiguration");
            var moc = mc.GetInstances();
            foreach (ManagementObject mo in moc)
            {
                if (originalDnsServers.TryGetValue((string)mo["Description"], out var original))
                {
                    if (log.IsDebugEnabled)
                    {
                        const string comma = ", ";
                        log.Debug($"restore {mo["Description"]}");
                        log.Debug($"dns {string.Join(comma, original)}");
                    }
                    var objdns = mo.GetMethodParameters("SetDNSServerSearchOrder");
                    objdns["DNSServerSearchOrder"] = original;
                    mo.InvokeMethod("SetDNSServerSearchOrder", objdns, null);
                }
            }
        }

        /// <summary>
        ///   Set the DNS server addresses of all network interfaces.
        /// </summary>
        /// <param name="server">
        ///   The sequence of <see cref="IPAddress"/> for the DNS server.
        /// </param>
        public void SetDnsServer(IEnumerable<IPAddress> addresses)
        {
            // TODO: IPv6
            if (addresses.Any(a => a.AddressFamily == AddressFamily.InterNetworkV6))
                throw new NotSupportedException("IPv6 addresses.");

            var stringAddresses = addresses.Select(a => a.ToString()).ToArray();

            var mc = new ManagementClass("Win32_NetworkAdapterConfiguration");
            var moc = mc.GetInstances();
            foreach (ManagementObject mo in moc)
            {
                if (!(bool)mo["IPEnabled"])
                {
                    continue;
                }
                if (log.IsDebugEnabled)
                {
                    log.Debug($"set {mo["Description"]}");
                    log.Debug("dns " + String.Join(", ", addresses));
                }

                var objdns = mo.GetMethodParameters("SetDNSServerSearchOrder");
                objdns["DNSServerSearchOrder"] = stringAddresses;
                var result = mo.InvokeMethod("SetDNSServerSearchOrder", objdns, null);
                var rv = (uint)result["ReturnValue"];
                if (rv != 0)
                    throw new Exception($"Failed to set dns server search order.  Error code {rv}.");

                originalDnsServers[(string)mo["Description"]] = (string[])mo["DNSServerSearchOrder"];
            }
        }
    }
}
