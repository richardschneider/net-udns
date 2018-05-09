# Asynchronous I/O

All requests to the DNS server are [asynchronous](https://docs.microsoft.com/en-us/dotnet/csharp/async),
which does not block current thread.

This means that callers should **normally** use the `async/await` paradigm

```csharp
Message response = await DnsClient.QueryAsync(...);
```

# Synchronous I/O
If a synchronous operation is required, then this can work

```csharp
Message response = DnsClient.QueryAsync(...).Result;
```
