# Cancelling a request

All requests to the DNS server can be cancelled by supplying 
an optional [CancellationToken](xref:System.Threading.CancellationToken).  When 
the token is cancelled, 
a [TaskCanceledException](xref:System.Threading.Tasks.TaskCanceledException) 
will be `thrown`.

Here's a contrived unit test that forces the query to be cancelled

```csharp
var cts = new CancellationTokenSource(500);
try
{
	await Task.Delay(400);
	var response = await DnsClient.QueryAsync(..., cts.Token);
	Assert.Fail("Did not throw TaskCanceledException");
}
catch (TaskCanceledException)
{
	return;
}
```

See also [Task Cancellation](https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/task-cancellation)
