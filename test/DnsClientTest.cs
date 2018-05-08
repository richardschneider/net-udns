using Makaretu.Dns;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace Makaretu.Dns
{
    
    [TestClass]
    public class DnsClientTest
    {
        [TestMethod]
        public void Servers()
        {
            var servers = DnsClient.GetServers().ToArray();
            Assert.AreNotEqual(0, servers.Length);
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
            DnsClient.TimeoutUdp = TimeSpan.FromMilliseconds(1);
            DnsClient.TimeoutTcp= TimeSpan.FromMilliseconds(1);
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
        public void Query_UnknownName()
        {
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = "emanon.foo", Type = DnsType.A });
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
            DnsClient.Servers = new IPAddress[]
            {
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
    }
}
