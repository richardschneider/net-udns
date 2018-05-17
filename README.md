# net-udns

[![build status](https://ci.appveyor.com/api/projects/status/github/richardschneider/net-udns?branch=master&svg=true)](https://ci.appveyor.com/project/richardschneider/net-udns) 
[![travis build](https://travis-ci.org/richardschneider/net-udns.svg?branch=master)](https://travis-ci.org/richardschneider/net-udns)
[![CircleCI](https://circleci.com/gh/richardschneider/net-udns.svg?style=svg)](https://circleci.com/gh/richardschneider/net-udns)
[![Coverage Status](https://coveralls.io/repos/richardschneider/net-udns/badge.svg?branch=master&service=github)](https://coveralls.io/github/richardschneider/net-udns?branch=master)
[![Version](https://img.shields.io/nuget/v/Makaretu.Dns.Unicast.svg)](https://www.nuget.org/packages/Makaretu.Dns.Unicast)
[![docs](https://cdn.rawgit.com/richardschneider/net-udns/master/doc/images/docs-latest-green.svg)](https://richardschneider.github.io/net-udns/articles/intro.html)

A DNS client that can fetch more than `A` and `AAAA` resource records.

## Features

- Uses UDP
- Fallbacks to TCP if no UDP response or the response is truncated
- Supports asynchronous I/O
- Supports cancellation
- Supports IPv6 and IPv4 platforms
- Targets .NET Standard 1.4 and 2.0
- CI on Circle (Debian GNU/Linux), Travis (Ubuntu Trusty) and AppVeyor (Windows Server 2016)

## Getting started

Published releases are available on [NuGet](https://www.nuget.org/packages/Makaretu.Dns.Unicast/).  To install, run the following command in the [Package Manager Console](https://docs.nuget.org/docs/start-here/using-the-package-manager-console).

    PM> Install-Package Makaretu.Dns.Unicast
    
## Usage

#### Get IP addresses

```csharp
using Makaretu.Dns;

var addresses = await DnsClient.ResolveAsync("cloudflare-dns.com");
foreach (var a in addresses)
    Console.WriteLine(a.ToString());
```

Produces the output
```
104.16.111.25
104.16.112.25
2400:cb00:2048:1::6810:6f19
2400:cb00:2048:1::6810:7019
````

#### Get all the TXT strings

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

# License
Copyright © 2018 Richard Schneider (makaretu@gmail.com)

The package is licensed under the [MIT](http://www.opensource.org/licenses/mit-license.php "Read more about the MIT license form") license. Refer to the [LICENSE](https://github.com/richardschneider/net-udns/blob/master/LICENSE) file for more information.
