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
    public class DohClientTest
    {
        [TestMethod]
        public void Server()
        {
            var doh = new DohClient();
            Assert.IsNotNull(doh.ServerUrl);
        }

        [TestMethod]
        public async Task Resolve()
        {
            var doh = new DohClient();
            var addresses = await doh.ResolveAsync("cloudflare-dns.com");
            Assert.AreNotEqual(0, addresses.Count());
            Assert.IsTrue(addresses.Any(a => a.AddressFamily == AddressFamily.InterNetwork));
            Assert.IsTrue(addresses.Any(a => a.AddressFamily == AddressFamily.InterNetworkV6));
        }

        [TestMethod]
        public void Resolve_Unknown()
        {
            var doh = new DohClient();
            ExceptionAssert.Throws<IOException>(() =>
            {
                var _ = doh.ResolveAsync("emanon.noname").Result;
            });
        }

        [TestMethod]
        public async Task Query()
        {
            var doh = new DohClient();
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "ipfs.io", Type = DnsType.TXT });
            var response = await doh.QueryAsync(query);
            Assert.IsNotNull(response);
            Assert.AreNotEqual(0, response.Answers.Count);
        }

        [TestMethod]
        [Ignore("not always timing out")]
        public void Query_Timeout()
        {
            var doh = new DohClient
            {
                Timeout = TimeSpan.FromMilliseconds(1)
            };
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "ipfs-x.io", Type = DnsType.TXT });
            ExceptionAssert.Throws<TaskCanceledException>(() =>
            {
                var _ = doh.QueryAsync(query).Result;
            });
        }

        [TestMethod]
        public void Query_UnknownTldName()
        {
            var doh = new DohClient();
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "emanon.foo", Type = DnsType.A });
            ExceptionAssert.Throws<IOException>(() =>
            {
                var _ = doh.QueryAsync(query).Result;
            }, "DNS error 'NameError'.");
        }

        [TestMethod]
        public void Query_UnknownName()
        {
            var doh = new DohClient();
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "emanon.noname.google.com", Type = DnsType.A });
            ExceptionAssert.Throws<IOException>(() =>
            {
                var _ = doh.QueryAsync(query).Result;
            }, "DNS error 'NameError'.");
        }

        [TestMethod]
        public void Query_UnknownName_NoThrow()
        {
            using (var doh = new DohClient { ThrowResponseError = false })
            {
                var query = new Message { RD = true };
                query.Questions.Add(new Question { Name = "emanon.noname.google.com", Type = DnsType.A });
                var result = doh.QueryAsync(query).Result;
                Assert.AreEqual(MessageStatus.NameError, result.Status);
            }
        }

        [TestMethod]
        public void Query_InvalidServer()
        {
            var doh = new DohClient
            {
                ServerUrl = "https://emanon.noname"
            };
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "emanon.noname.google.com", Type = DnsType.A });
            ExceptionAssert.Throws<Exception>(() =>
            {
                var _ = doh.QueryAsync(query).Result;
            });
        }

        [TestMethod]
        public async Task Reverse()
        {
            var doh = new DohClient();
            var name = await doh.ResolveAsync(IPAddress.Parse("1.1.1.1"));
            Assert.AreEqual("1dot1dot1dot1.cloudflare-dns.com", name);

            name = await doh.ResolveAsync(IPAddress.Parse("2606:4700:4700::1111"));
            Assert.AreEqual("1dot1dot1dot1.cloudflare-dns.com", name);
        }

        [TestMethod]
        public async Task Resolve_Reverse()
        {
            var doh = new DohClient();
            var github = "github.com";
            var addresses = await doh.ResolveAsync(github);
            foreach (var address in addresses)
            {
                var name = await doh.ResolveAsync(address);
                StringAssert.EndsWith(name, github);
            }
        }

        [TestMethod]
        public async Task Query_GoogleServer()
        {
            var doh = new DohClient
            {
                ServerUrl = "https://dns.google.com/experimental"
            };
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "ipfs.io", Type = DnsType.TXT });
            var response = await doh.QueryAsync(query);
            Assert.IsNotNull(response);
            Assert.AreNotEqual(0, response.Answers.Count);
        }

        [TestMethod]
        [Ignore("doh.securedns.eu now requires authentication")]
        public async Task Query_SecureEuServer()
        {
            var doh = new DohClient
            {
                ServerUrl = "https://doh.securedns.eu/dns-query"
            };
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "ipfs.io", Type = DnsType.TXT });
            var response = await doh.QueryAsync(query);
            Assert.IsNotNull(response);
            Assert.AreNotEqual(0, response.Answers.Count);
        }

        [TestMethod]
        [Ignore("doh.securedns.eu now requires authentication")]
        public async Task Query_EDNS()
        {
            var doh = new DohClient
            {
                ServerUrl = "https://doh.securedns.eu/dns-query"
            };
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
            var response = await doh.QueryAsync(query);
            Assert.IsNotNull(response);
            Assert.AreNotEqual(0, response.Answers.Count);
        }

    }
}
