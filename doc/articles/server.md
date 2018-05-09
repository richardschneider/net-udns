# Finding a DNS server

## Default

Normally the OS network interfaces are queried for the DNS servers; see [GetServers](xref:Makaretu.Dns.DnsClient.GetServers) for the details.

## Specific

To specify the servers, set the [DnsClient.Servers](xref:Makaretu.Dns.DnsClient.Servers) property

```csharp
DnsClient.Servers = new IPAddress[] 
{
    IPAddress.Parse("8.8.8.8"),
    IPAddress.Parse("2001:4860:4860::8888")
}; 
```
