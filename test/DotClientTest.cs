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
    public class DotClientTest
    {
        [TestMethod]
        public void PublicServers()
        {
            var dot = new DotClient();
            Assert.AreNotEqual(0, DotClient.PublicServers.Length);
        }

        [TestMethod]
        public void Resolve()
        {
            var dot = new DotClient();
            var addresses = dot.ResolveAsync("github.com").Result.ToArray();
            Assert.AreNotEqual(0, addresses.Length);

            addresses = dot.ResolveAsync("ipfs.io").Result.ToArray();
            Assert.AreNotEqual(0, addresses.Length);
        }

        [TestMethod]
        public void Resolve_Unknown()
        {
            var dot = new DotClient();
            ExceptionAssert.Throws<IOException>(() =>
            {
                var _ = dot.ResolveAsync("emanon.noname").Result;
            });
        }

        [TestMethod]
        public async Task Query()
        {
            var dot = new DotClient();
            var query = new Message { RD = true, Id = 0x1234 };
            query.Questions.Add(new Question { Name = "ipfs.io", Type = DnsType.TXT });
            var response = await dot.QueryAsync(query);
            Assert.IsNotNull(response);
            Assert.AreNotEqual(0, response.Answers.Count);
        }

        [TestMethod]
        public async Task Query_Stream_Closes()
        {
            var dot = new DotClient();
            var query = new Message { RD = true, Id = 0x1234 };
            query.Questions.Add(new Question { Name = "ipfs.io", Type = DnsType.TXT });
            var response = await dot.QueryAsync(query);
            Assert.IsNotNull(response);
            Assert.AreNotEqual(0, response.Answers.Count);

            (await dot.GetDnsServerAsync()).Dispose();
            response = await dot.QueryAsync(query);
            Assert.IsNotNull(response);
            Assert.AreNotEqual(0, response.Answers.Count);
        }

        [TestMethod]
        public void Query_UnknownTldName()
        {
            var dot = new DotClient();
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "emanon.foo", Type = DnsType.A });
            ExceptionAssert.Throws<IOException>(() =>
            {
                var _ = dot.QueryAsync(query).Result;
            }, "DNS error 'NameError'.");
        }

        [TestMethod]
        public void Query_UnknownName()
        {
            var dot = new DotClient();
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "emanon.noname.google.com", Type = DnsType.A });
            ExceptionAssert.Throws<IOException>(() =>
            {
                var _ = dot.QueryAsync(query).Result;
            }, "DNS error 'NameError'.");
        }



        [TestMethod]
        public void Query_InvalidServer_IPAddress()
        {
            var dot = new DotClient
            {
                Servers = new[]
                {
                    new DotEndPoint { Address = IPAddress.Any }
                }
            };
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "emanon.noname.google.com", Type = DnsType.A });
            ExceptionAssert.Throws<Exception>(() =>
            {
                var _ = dot.QueryAsync(query).Result;
            });
        }

        [TestMethod]
        public void Query_InvalidServer_Hostname()
        {
            var dot = new DotClient
            {
                Servers = new[]
                {
                    new DotEndPoint
                    {
                        Hostname = "bad-cloudflare-dns.com", // bad
                        Address = IPAddress.Parse("1.1.1.1")
                    }
                }
            };
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "emanon.noname.google.com", Type = DnsType.A });
            ExceptionAssert.Throws<Exception>(() =>
            {
                var _ = dot.QueryAsync(query).Result;
            });
        }

        [TestMethod]
        public async Task Query_OneDeadServer()
        {
            var dot = new DotClient
            {
                Servers = new[]
                {
                    new DotEndPoint
                    {
                        Address = IPAddress.Parse("127.0.0.1"),
                        Port = 8530
                    },
                    DotClient.PublicServers[0]
                }
            };
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "ipfs.io", Type = DnsType.TXT });
            var response = await dot.QueryAsync(query);
            Assert.IsNotNull(response);
            Assert.AreNotEqual(0, response.Answers.Count);
        }

        [TestMethod]
        public void NoSerer()
        {
            var dot = new DotClient
            {
                Servers = new DotEndPoint[0]
            };
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "ipfs.io", Type = DnsType.TXT });
            ExceptionAssert.Throws<Exception>(() =>
            {
                var _ = dot.QueryAsync(query).Result;
            });
        }

        [TestMethod]
        public async Task Reverse()
        {
            var dot = new DotClient();
            var name = await dot.ResolveAsync(IPAddress.Parse("1.1.1.1"));
            Assert.AreEqual("1dot1dot1dot1.cloudflare-dns.com", name);

            name = await dot.ResolveAsync(IPAddress.Parse("2606:4700:4700::1111"));
            Assert.AreEqual("1dot1dot1dot1.cloudflare-dns.com", name);
        }

        [TestMethod]
        public async Task Resolve_Reverse()
        {
            var dot = new DotClient();
            var github = "github.com";
            var addresses = await dot.ResolveAsync(github);
            foreach (var address in addresses)
            {
                var name = await dot.ResolveAsync(address);
                StringAssert.EndsWith(name, github);
            }
        }

        [TestMethod]
        public async Task Query_Quad9()
        {
            var dot = new DotClient
            {
                Servers = new[]
                {
                    new DotEndPoint
                    {
                        Hostname = "dns.quad9.net",
                        Address = IPAddress.Parse("9.9.9.9")
                    }
                }
            };
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "ipfs.io", Type = DnsType.TXT });
            var response = await dot.QueryAsync(query);
            Assert.IsNotNull(response);
            Assert.AreNotEqual(0, response.Answers.Count);
        }

        [TestMethod]
        public async Task Query_SecureEU()
        {
            var dot = new DotClient
            {
                Servers = new[]
                {
                    new DotEndPoint
                    {
                        Hostname = "dot.securedns.eu",
                        Pins = new[] { "h3mufC43MEqRD6uE4lz6gAgULZ5/riqH/E+U+jE3H8g=" },
                        Address = IPAddress.Parse("146.185.167.43")
                    },
                }
            };
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "ipfs.io", Type = DnsType.TXT });
            var response = await dot.QueryAsync(query);
            Assert.IsNotNull(response);
            Assert.AreNotEqual(0, response.Answers.Count);
        }

    }
}
