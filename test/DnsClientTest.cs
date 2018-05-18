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
    public class DnsClientTest
    {
        static bool SupportsIPv6
        {
            get
            {
                return Socket.OSSupportsIPv6

                    // See https://discuss.circleci.com/t/ipv6-support/13571
                    && Environment.GetEnvironmentVariable("CIRCLECI") == null

                    // See https://github.com/njh/travis-ipv6-test
                    && Environment.GetEnvironmentVariable("TRAVIS") == null

                    && Environment.GetEnvironmentVariable("APPVEYOR") == null
                    ;
            }
        }
        [TestMethod]
        public void Servers()
        {
            var servers = DnsClient.GetServers().ToArray();
            Assert.AreNotEqual(0, servers.Length);
        }

        [TestMethod]
        public void Resolve()
        {
            var addresses = DnsClient.ResolveAsync("cloudflare-dns.com").Result.ToArray();
            Assert.AreNotEqual(0, addresses.Length);
            Assert.IsTrue(addresses.Any(a => a.AddressFamily == AddressFamily.InterNetwork));
            Assert.IsTrue(addresses.Any(a => a.AddressFamily == AddressFamily.InterNetworkV6));
        }

        [TestMethod]
        public void Resolve_Unknown()
        {
            ExceptionAssert.Throws<IOException>(() =>
            {
                var _ = DnsClient.ResolveAsync("emanon.noname").Result;
            });
        }

        [TestMethod]
        public async Task Query()
        {
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "ipfs.io", Type = DnsType.TXT });
            var response = await DnsClient.QueryAsync(query);
            Assert.IsNotNull(response);
            Assert.AreNotEqual(0, response.Answers.Count);
        }

        [TestMethod]
        public void Query_Timeout()
        {
            var originalUdp = DnsClient.TimeoutUdp;
            var originalTcp = DnsClient.TimeoutTcp;
            DnsClient.TimeoutUdp = TimeSpan.FromMilliseconds(0);
            DnsClient.TimeoutTcp= TimeSpan.FromMilliseconds(0);
            try
            {
                var query = new Message { RD = true };
                query.Questions.Add(new Question { Name = "ipfs.io", Type = DnsType.TXT });
                ExceptionAssert.Throws<IOException>(() =>
                {
                    var _ = DnsClient.QueryAsync(query).Result;
                }, "No response from DNS servers.");
            }
            finally
            {
                DnsClient.TimeoutUdp = originalUdp;
                DnsClient.TimeoutTcp = originalTcp;
            }
        }

        [TestMethod]
        public async Task Query_UdpTimeout()
        {
            var originalUdp = DnsClient.TimeoutUdp;
            DnsClient.TimeoutUdp = TimeSpan.FromMilliseconds(1);
            DnsClient.Servers = new IPAddress[] { IPAddress.Parse("8.8.8.8") }; // google supports TCP!
            try
            {
                var query = new Message { RD = true };
                query.Questions.Add(new Question { Name = "ipfs.io", Type = DnsType.TXT });
                var response = await DnsClient.QueryAsync(query);
                Assert.IsNotNull(response);
                Assert.AreNotEqual(0, response.Answers.Count);
            }
            finally
            {
                DnsClient.TimeoutUdp = originalUdp;
                DnsClient.Servers = null;
            }
        }

        [TestMethod]
        public void Query_UnknownTldName()
        {
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "emanon.foo", Type = DnsType.A });
            ExceptionAssert.Throws<IOException>(() =>
            {
                var _ = DnsClient.QueryAsync(query).Result;
            }, "DNS error 'NameError'.");
        }

        [TestMethod]
        public void Query_UnknownName()
        {
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "emanon.noname.google.com", Type = DnsType.A });
            ExceptionAssert.Throws<IOException>(() =>
            {
                var _ = DnsClient.QueryAsync(query).Result;
            }, "DNS error 'NameError'.");
        }

        [TestMethod]
        public async Task Query_OneDeadServer()
        {
            DnsClient.Servers = new IPAddress[] 
            {
                IPAddress.Parse("127.0.0.1"),
                IPAddress.Parse("8.8.8.8")
            }; 
            try
            {
                var query = new Message { RD = true };
                query.Questions.Add(new Question { Name = "ipfs.io", Type = DnsType.TXT });
                var response = await DnsClient.QueryAsync(query);
                Assert.IsNotNull(response);
                Assert.AreNotEqual(0, response.Answers.Count);
            }
            finally
            {
                DnsClient.Servers = null;
            }
        }

        [TestMethod]
        public async Task Query_IPv6()
        {
            if (!SupportsIPv6)
            {
                Assert.Inconclusive("IPv6 not supported by OS.");
            }

            DnsClient.Servers = new IPAddress[]
            {
                IPAddress.Parse("2606:4700:4700::1111"), // cloudflare dns
                IPAddress.Parse("2001:4860:4860::8888"), // google dns
            };
            try
            {
                var query = new Message { RD = true };
                query.Questions.Add(new Question { Name = "ipfs.io", Type = DnsType.TXT });
                var response = await DnsClient.QueryAsync(query);
                Assert.IsNotNull(response);
                Assert.AreNotEqual(0, response.Answers.Count);
            }
            finally
            {
                DnsClient.Servers = null;
            }
        }

        [TestMethod]
        public void Query_NoServers()
        {
            DnsClient.Servers = new IPAddress[0];
            try
            {
                var query = new Message { RD = true };
                query.Questions.Add(new Question { Name = "emanon.noname.google.com", Type = DnsType.A });
                ExceptionAssert.Throws<Exception>(() =>
                {
                    var _ = DnsClient.QueryAsync(query).Result;
                }, "No DNS servers are available.");
            }
            finally
            {
                DnsClient.Servers = null;
            }
        }

        [TestMethod]
        public void Query_UnreachableServer()
        {
            DnsClient.Servers = new IPAddress[] { IPAddress.Parse("127.0.0.1") };
            try
            {
                ExceptionAssert.Throws<Exception>(() =>
                {
                    var _ = DnsClient.QueryAsync("ipfs.io", DnsType.A).Result;
                }, "No response from DNS servers.");
            }
            finally
            {
                DnsClient.Servers = null;
            }
        }

        [TestMethod]
        public async Task Reverse()
        {
            var name = await DnsClient.QueryAsync(IPAddress.Parse("1.1.1.1"));
            Assert.AreEqual("1dot1dot1dot1.cloudflare-dns.com", name);

            name = await DnsClient.QueryAsync(IPAddress.Parse("2606:4700:4700::1111"));
            Assert.AreEqual("1dot1dot1dot1.cloudflare-dns.com", name);
        }

        [TestMethod]
        public async Task Resolve_Reverse()
        {
            var github = "github.com";
            var addresses = await DnsClient.ResolveAsync(github);
            foreach (var address in addresses)
            {
                var name = await DnsClient.QueryAsync(address);
                StringAssert.EndsWith(name, github);
            }
        }
    }
}
