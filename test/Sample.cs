using Makaretu.Dns;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Makaretu.Dns
{
    
    [TestClass]
    public class Sample
    {

        [TestMethod]
        public async Task TXT()
        {
            var dns = new DnsClient();
            var response = await dns.QueryAsync("ipfs.io", DnsType.TXT);
            var strings = response.Answers
                .OfType<TXTRecord>()
                .SelectMany(txt => txt.Strings);
            foreach (var s in strings)
                Console.WriteLine(s);
        }

        [TestMethod]
        public async Task Resolve()
        {
            var dns = new DnsClient();
            var addresses = await dns.ResolveAsync("cloudflare-dns.com");
            foreach (var a in addresses)
                Console.WriteLine(a.ToString());
        }

        [TestMethod]
        public async Task SecureQueryAddress()
        {
            var dns = new DnsClient();
            var response = await dns.SecureQueryAsync("dia.govt.nz", DnsType.A);
            foreach (var a in response.Answers)
                Console.WriteLine(a.ToString());
        }

        [TestMethod]
        public async Task SecureQueryDnsKey()
        {
            var dns = new DnsClient();
            var response = await dns.SecureQueryAsync("dia.govt.nz", DnsType.DNSKEY);
            foreach (var a in response.Answers)
                Console.WriteLine(a.ToString());
        }

        [TestMethod]
        public async Task SecureQueryDs()
        {
            var dns = new DnsClient();
            var response = await dns.SecureQueryAsync("govt.nz", DnsType.DS);
            foreach (var a in response.Answers)
                Console.WriteLine(a.ToString());
        }
    }
}
