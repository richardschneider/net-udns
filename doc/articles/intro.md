# DNS Clients

Various DNS clients that communicate with a DNS server. The source code is on [GitHub](https://github.com/richardschneider/net-udns) and the package is published on [NuGet](https://www.nuget.org/packages/Makaretu.Dns.Unicast).

- [DnsClient](xref:Makaretu.Dns.DnsClient) is used to send a [Message](xref:Makaretu.Dns.Message) 
  to a standard DNS server and receive a response; uses UPD and then TCP (if needed).
- [DohClient](xref:Makaretu.Dns.DohClient) is used to send a [Message](xref:Makaretu.Dns.Message) 
  to a DNS over HTTPS server and receive a response.

## Usage

Get all the TXT strings assoicated with "ipfs.io"

```csharp
using Makaretu.Dns;

var response = await DnsClient.QueryAsync("ipfs.io", DnsType.TXT);
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

