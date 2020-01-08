# Bedrock Framework

[![feedz.io](https://img.shields.io/badge/endpoint.svg?url=https%3A%2F%2Ff.feedz.io%2Fdavidfowl%2Fbedrockframework%2Fshield%2FBedrock.Framework%2Flatest&label=Bedrock.Framework)](https://f.feedz.io/davidfowl/bedrockframework/packages/Bedrock.Framework/latest/download)

[Project Bedrock](https://github.com/aspnet/AspNetCore/issues/4772) is a set of .NET Core APIs for doing transport agnostic networking. In .NET Core 3.0 we've introduced some new abstractions
as part of [Microsoft.AspNetCore.Connections.Abstractions](https://www.nuget.org/packages/Microsoft.AspNetCore.Connections.Abstractions) for client-server communication.

This project is split into 2 packages:
- **Bedrock.Framework** - The core framework, server and client builder APIs, built in middleware and transports (sockets and memory).
- **Bedrock.Framework.Experimental** - A set of protocol and transport implementations that may eventually make their way into core. Some of them are incomplete at this time.

## Using CI builds

To use CI builds add the following nuget feed:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <packageSources>
        <clear />
        <add key="bedrockframework" value="https://f.feedz.io/davidfowl/bedrockframework/nuget/index.json" />
        <add key="NuGet.org" value="https://api.nuget.org/v3/index.json" />
    </packageSources>
</configuration>
```
