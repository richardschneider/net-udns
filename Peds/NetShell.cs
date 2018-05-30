using Common.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Peds
{
    /// <summary>
    ///   Builds a list of 'netsh' commands and then executes them
    ///   all at once.
    /// </summary>
    class NetShell
    {
        static ILog log = LogManager.GetLogger("netsh");

        StreamWriter commands;
        string commandsFilename = Path.GetTempFileName();

        /// <summary>
        ///   Set the DNS servers for a network interface.
        /// </summary>
        /// <param name="nic">
        ///   The network interface.
        /// </param>
        /// <param name="addresses">
        ///   The <see cref="IPAddress"/> sequence for the DNS server. 
        /// </param>
        public void SetDnsServers(NetworkInterface nic, IEnumerable<IPAddress> addresses)
        {
            string qname = "\"" + nic.Name + "\"";

            // IPv4
            var addrs = addresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork);
            var primary = true;
            foreach (var addr in addrs)
            {
                if (primary)
                {
                    Run($"interface ipv4 delete dns {qname} all");
                }
                Run($"interface ipv4 add dns {qname} {addr} validate=no");
                primary = false;
            }

            // IPv6
            addrs = addresses.Where(a => a.AddressFamily == AddressFamily.InterNetworkV6);
            primary = true;
            foreach (var addr in addrs)
            {
                if (primary)
                {
                    Run($"interface ipv6 delete dns {qname} all");
                }
                Run($"interface ipv6 add dns {qname} {addr} validate=no");
                primary = false;
            }
        }

        void Run(string args)
        {
            log.Debug(args);
            if (commands == null)
            {
                commands = new StreamWriter(File.OpenWrite(commandsFilename));
            }
            commands.WriteLine(args);
        }

        /// <summary>
        ///   Run the netsh commands.
        /// </summary>
        public void Run()
        {
            if (commands == null)
                return;
            commands.Close();
            commands = null;

            var args = $"-f \"{commandsFilename}\"";
            var netsh = Process.Start(new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = args,
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
            netsh.WaitForExit(1000);
            File.Delete(commandsFilename);
            if (netsh.ExitCode != 0)
            {
                throw new Exception($"netsh failed with exit code {netsh.ExitCode}.");
            }
        }

    }
}
