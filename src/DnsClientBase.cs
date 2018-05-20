using Common.Logging;
using Nito.AsyncEx; 
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Makaretu.Dns
{
    /// <summary>
    ///   Base class for a DNS client.
    /// </summary>
    /// <remarks>
    ///   Sends and receives DNS queries and answers to unicast DNS servers.
    /// </remarks>
    public abstract class DnsClientBase : IDnsClient
    {
        /// <inheritdoc />
        public async Task<IEnumerable<IPAddress>> ResolveAsync(
            string name,
            CancellationToken cancel = default(CancellationToken))
        {
            var a = QueryAsync(name, DnsType.A, cancel);
            var aaaa = QueryAsync(name, DnsType.AAAA, cancel);
            var responses = await Task.WhenAll(a, aaaa);
            return responses
                .SelectMany(m => m.Answers)
                .Where(rr => rr.Type == DnsType.A || rr.Type == DnsType.AAAA)
                .Select(rr => rr.Type == DnsType.A
                    ? ((ARecord)rr).Address
                    : ((AAAARecord)rr).Address);
        }

        /// <inheritdoc />
        public Task<Message> QueryAsync(
            string name,
            DnsType rtype,
            CancellationToken cancel = default(CancellationToken))
        {
            var query = new Message { RD = true };
            query.Questions.Add(new Question { Name = name, Type = rtype });

            return QueryAsync(query, cancel);
        }

        /// <inheritdoc />
        public async Task<string> ResolveAsync(
            IPAddress address,
            CancellationToken cancel = default(CancellationToken))
        {
            var response = await QueryAsync(address.GetArpaName(), DnsType.PTR);
            return response.Answers
                .OfType<PTRRecord>()
                .Select(p => p.DomainName)
                .First();
        }

        /// <inheritdoc />
        public abstract Task<Message> QueryAsync(
            Message request,
            CancellationToken cancel = default(CancellationToken));
    }
}
