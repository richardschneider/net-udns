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
            Assert.IsNotNull(DohClient.ServerUrl);
        }

        [TestMethod]
        public void Resolve()
        {
            var addresses = DohClient.ResolveAsync("cloudflare-dns.com").Result.ToArray();
            Assert.AreNotEqual(0, addresses.Length);
            Assert.IsTrue(addresses.Any(a => a.AddressFamily == AddressFamily.InterNetwork));
            Assert.IsTrue(addresses.Any(a => a.AddressFamily == AddressFamily.InterNetworkV6));
        }

        [TestMethod]
        public void Resolve_Unknown()
        {
            ExceptionAssert.Throws<IOException>(() =>
            {
                var _ = DohClient.ResolveAsync("emanon.noname").Result;
            });
        }

        [TestMethod]
        public async Task Query()
        {
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "ipfs.io", Type = DnsType.TXT });
            var response = await DohClient.QueryAsync(query);
            Assert.IsNotNull(response);
            Assert.AreNotEqual(0, response.Answers.Count);
        }

        [TestMethod]
        public void Query_Timeout()
        {
            var original = DohClient.Timeout;
            DohClient.Timeout = TimeSpan.FromMilliseconds(0);
            try
            {
                var query = new Message { RD = true };
                query.Questions.Add(new Question { Name = "ipfs.io", Type = DnsType.TXT });
                ExceptionAssert.Throws<TaskCanceledException>(() =>
                {
                    var _ = DohClient.QueryAsync(query).Result;
                });
            }
            finally
            {
                DohClient.Timeout = original;
            }
        }

        [TestMethod]
        public void Query_UnknownTldName()
        {
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "emanon.foo", Type = DnsType.A });
            ExceptionAssert.Throws<IOException>(() =>
            {
                var _ = DohClient.QueryAsync(query).Result;
            }, "DNS error 'NameError'.");
        }

        [TestMethod]
        public void Query_UnknownName()
        {
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "emanon.noname.google.com", Type = DnsType.A });
            ExceptionAssert.Throws<IOException>(() =>
            {
                var _ = DohClient.QueryAsync(query).Result;
            }, "DNS error 'NameError'.");
        }



        [TestMethod]
        public void Query_InvalidServer()
        {
            var original = DohClient.ServerUrl;
            DohClient.ServerUrl = "https://emanon.noname";
            try
            {
                var query = new Message { RD = true };
                query.Questions.Add(new Question { Name = "emanon.noname.google.com", Type = DnsType.A });
                ExceptionAssert.Throws<Exception>(() =>
                {
                    var _ = DohClient.QueryAsync(query).Result;
                });
            }
            finally
            {
                DohClient.ServerUrl = original;
            }
        }

        [TestMethod]
        public async Task Reverse()
        {
            var name = await DohClient.QueryAsync(IPAddress.Parse("1.1.1.1"));
            Assert.AreEqual("1dot1dot1dot1.cloudflare-dns.com", name);

            name = await DohClient.QueryAsync(IPAddress.Parse("2606:4700:4700::1111"));
            Assert.AreEqual("1dot1dot1dot1.cloudflare-dns.com", name);
        }

        [TestMethod]
        public async Task Resolve_Reverse()
        {
            var github = "github.com";
            var addresses = await DohClient.ResolveAsync(github);
            foreach (var address in addresses)
            {
                var name = await DohClient.QueryAsync(address);
                StringAssert.EndsWith(name, github);
            }
        }

        [TestMethod]
        public async Task Query_GoogleServer()
        {
            var original = DohClient.ServerUrl;
            DohClient.ServerUrl = "https://dns.google.com/experimental";
            try
            {
                var query = new Message { RD = true };
                query.Questions.Add(new Question { Name = "ipfs.io", Type = DnsType.TXT });
                var response = await DohClient.QueryAsync(query);
                Assert.IsNotNull(response);
                Assert.AreNotEqual(0, response.Answers.Count);
            }
            finally
            {
                DohClient.ServerUrl = original;
            }
        }

        [TestMethod]
        public async Task Query_SecureEuServer()
        {
            var original = DohClient.ServerUrl;
            DohClient.ServerUrl = "https://doh.securedns.eu/dns-query";
            try
            {
                var query = new Message { RD = true };
                query.Questions.Add(new Question { Name = "ipfs.io", Type = DnsType.TXT });
                var response = await DohClient.QueryAsync(query);
                Assert.IsNotNull(response);
                Assert.AreNotEqual(0, response.Answers.Count);
            }
            finally
            {
                DohClient.ServerUrl = original;
            }
        }

    }
}
