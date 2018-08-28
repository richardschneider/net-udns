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
            var dns = new DnsClient();
            var servers = dns.GetServers().ToArray();
            Assert.AreNotEqual(0, servers.Length);
        }

        [TestMethod]
        public void Resolve()
        {
            var dns = new DnsClient();
            var addresses = dns.ResolveAsync("cloudflare-dns.com").Result.ToArray();
            Assert.AreNotEqual(0, addresses.Length);
            Assert.IsTrue(addresses.Any(a => a.AddressFamily == AddressFamily.InterNetwork));
            Assert.IsTrue(addresses.Any(a => a.AddressFamily == AddressFamily.InterNetworkV6));
        }

        [TestMethod]
        public void Resolve_Unknown()
        {
            var dns = new DnsClient();
            ExceptionAssert.Throws<IOException>(() =>
            {
                var _ = dns.ResolveAsync("emanon.noname").Result;
            });
        }

        [TestMethod]
        public async Task Query()
        {
            var dns = new DnsClient();
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "ipfs.io", Type = DnsType.TXT });
            var response = await dns.QueryAsync(query);
            Assert.IsNotNull(response);
            Assert.AreNotEqual(0, response.Answers.Count);
        }

        [TestMethod]
        public async Task SecureQuery_HasRRs()
        {
            var dns = new DnsClient();
            var query = new Message { RD = true }.UseDnsSecurity();
            query.Questions.Add(new Question { Name = "cloudflare-dns.com", Type = DnsType.AAAA });
            var response = await dns.QueryAsync(query);
            Assert.IsNotNull(response);
            Assert.AreNotEqual(0, response.Answers.Count);

            var opt = response.AdditionalRecords.OfType<OPTRecord>().Single();
            Assert.AreEqual(true, opt.DO);

            var rrsig = response.Answers.OfType<RRSIGRecord>().Single();
            Assert.AreEqual(DnsType.AAAA, rrsig.TypeCovered);
        }


        [TestMethod]
        [Ignore("not always timing out")]
        public void Query_Timeout()
        {
            var dns = new DnsClient
            {
                TimeoutUdp = TimeSpan.FromMilliseconds(1),
                TimeoutTcp = TimeSpan.FromMilliseconds(1)
            };
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "ipfs-x.io", Type = DnsType.TXT });
            ExceptionAssert.Throws<IOException>(() =>
            {
                var _ = dns.QueryAsync(query).Result;
            }, "No response from DNS servers.");
        }

        [TestMethod]
        public async Task Query_UdpTimeout()
        {
            var dns = new DnsClient
            {
                TimeoutUdp = TimeSpan.FromMilliseconds(0),
                Servers = new IPAddress[] { IPAddress.Parse("8.8.8.8") } // google supports TCP!
            };
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "ipfs.io", Type = DnsType.TXT });
            var response = await dns.QueryAsync(query);
            Assert.IsNotNull(response);
            Assert.AreNotEqual(0, response.Answers.Count);
        }

        [TestMethod]
        public void Query_UnknownTldName()
        {
            var dns = new DnsClient();
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "emanon.foo", Type = DnsType.A });
            ExceptionAssert.Throws<IOException>(() =>
            {
                var _ = dns.QueryAsync(query).Result;
            }, "DNS error 'NameError'.");
        }

        [TestMethod]
        public void Query_UnknownName()
        {
            var dns = new DnsClient();
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "emanon.noname.google.com", Type = DnsType.A });
            ExceptionAssert.Throws<IOException>(() =>
            {
                var _ = dns.QueryAsync(query).Result;
            }, "DNS error 'NameError'.");
        }

        [TestMethod]
        public void Query_UnknownName_NoThrow()
        {
            using (var dns = new DnsClient { ThrowResponseError = false })
            {
                var query = new Message { RD = true };
                query.Questions.Add(new Question { Name = "emanon.noname.google.com", Type = DnsType.A });
                var result = dns.QueryAsync(query).Result;
                Assert.AreEqual(MessageStatus.NameError, result.Status);
            }
        }

        [TestMethod]
        public async Task Query_OneDeadServer()
        {
            var dns = new DnsClient
            {
                Servers = new IPAddress[]
                {
                    IPAddress.Parse("127.0.0.1"),
                    IPAddress.Parse("8.8.8.8")
                }
            };
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "ipfs.io", Type = DnsType.TXT });
            var response = await dns.QueryAsync(query);
            Assert.IsNotNull(response);
            Assert.AreNotEqual(0, response.Answers.Count);
        }

        [TestMethod]
        public async Task Query_IPv6()
        {
            if (!SupportsIPv6)
            {
                Assert.Inconclusive("IPv6 not supported by OS.");
            }

            var dns = new DnsClient
            {
                Servers = new IPAddress[]
                {
                    IPAddress.Parse("2606:4700:4700::1111"), // cloudflare dns
                    IPAddress.Parse("2001:4860:4860::8888"), // google dns
                }
            };
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "ipfs.io", Type = DnsType.TXT });
            var response = await dns.QueryAsync(query);
            Assert.IsNotNull(response);
            Assert.AreNotEqual(0, response.Answers.Count);
        }

        [TestMethod]
        public void Query_NoServers()
        {
            var dns = new DnsClient
            {
                Servers = new IPAddress[0]
            };
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "emanon.noname.google.com", Type = DnsType.A });
            ExceptionAssert.Throws<Exception>(() =>
            {
                var _ = dns.QueryAsync(query).Result;
            }, "No DNS servers are available.");
        }

        [TestMethod]
        public void Query_UnreachableServer()
        {
            var dns = new DnsClient
            {
                Servers = new IPAddress[] { IPAddress.Parse("127.0.0.2") }
            };
            ExceptionAssert.Throws<Exception>(() =>
            {
                var _ = dns.QueryAsync("ipfs.io", DnsType.A).Result;
            }, "No response from DNS servers.");
        }

        [TestMethod]
        public async Task Reverse()
        {
            var dns = new DnsClient();
            var name = await dns.ResolveAsync(IPAddress.Parse("1.1.1.1"));
            Assert.AreEqual("one.one.one.one", name);

            name = await dns.ResolveAsync(IPAddress.Parse("2606:4700:4700::1111"));
            Assert.AreEqual("one.one.one.one", name);
        }

        [TestMethod]
        public async Task Resolve_Reverse()
        {
            var dns = new DnsClient();
            var github = "github.com";
            var addresses = await dns.ResolveAsync(github);
            foreach (var address in addresses)
            {
                var name = await dns.ResolveAsync(address);
                StringAssert.EndsWith(name, github);
            }
        }

        [TestMethod]
        public async Task Query_EDNS()
        {
            var dns = new DnsClient();
            var query = new Message
            {
                RD = true,
                Questions =
                {
                    new Question { Name = "ipfs.io", Type = DnsType.TXT }
                },
                AdditionalRecords =
                {
                    new OPTRecord
                    {
                        DO = true,
                        Options =
                        {
                            new EdnsNSIDOption(),
                            new EdnsKeepaliveOption(),
                            new EdnsPaddingOption { Padding = new byte[] {0, 0, 0, 0 } }
                        }
                    }
                }
            };
            var response = await dns.QueryAsync(query);
            Assert.IsNotNull(response);
            Assert.AreNotEqual(0, response.Answers.Count);
        }

    }
}
