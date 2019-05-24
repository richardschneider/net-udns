# DNS Clients

This package provides various unicast DNS clients, stub resolvers, that communicate with a DNS server. The source code is on [GitHub](https://github.com/richardschneider/net-udns) and the package is published on [NuGet](https://www.nuget.org/packages/Makaretu.Dns.Unicast).

- [DnsClient](xref:Makaretu.Dns.DnsClient) is used to send a [Message](xref:Makaretu.Dns.Message) 
  to a standard DNS server and receive a response; uses UPD and then TCP (if needed). There is no privacy.

- [DohClient](xref:Makaretu.Dns.DohClient), DNS over HTTPS (DoH), is an experimental protocol for performing remote 
Domain Name System (DNS) resolution via the HTTPS protocol. The goal
is to increase user privacy and security by preventing eavesdropping and 
manipulation of DNS data by man-in-the-middle attacks.

- [DotClient](xref:Makaretu.Dns.DotClient), DNS over TLS (DoT), is a security protocol for encrypting and wrapping 
DNS queries and answers via the Transport Layer Security (TLS) protocol. The goal 
is to increase user privacy and security by preventing eavesdropping and 
manipulation of DNS data via man-in-the-middle attacks.

## Usage

Get all the TXT strings assoicated with "ipfs.io"

```csharp
using Makaretu.Dns;

var dns = new DnsClient();
var response = await dns.QueryAsync("ipfs.io", DnsType.TXT);
var strings = response.Answers
    .OfType<TXTRecord>()
    .SelectMany(txt => txt.Strings);
foreach (var s in strings)
    Console.WriteLine(s);
```

Produces the output
```
dnslink=/ipfs/QmYNQJoKGNHTpPxCBPh9KkDpaExgd2duMa3aF6ytMpHdao
```

