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
            var response = await DnsClient.QueryAsync("ipfs.io", DnsType.TXT);
            var strings = response.Answers
                .OfType<TXTRecord>()
                .SelectMany(txt => txt.Strings);
            foreach (var s in strings)
                Console.WriteLine(s);
        }

    }
}
