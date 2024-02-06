using System;

namespace Bedrock.Framework;

public static partial class ServerBuilderExtensions
{
    public static ServerBuilder UseNamedPipes(this ServerBuilder serverBuilder, Action<NamedPipeServerBuilder> configure)
    {
        var namedPipeBuilder = new NamedPipeServerBuilder();
        configure(namedPipeBuilder);
        namedPipeBuilder.Apply(serverBuilder);
        return serverBuilder;
    }

    public static ClientBuilder UseNamedPipes(this ClientBuilder clientBuilder)
    {
        return clientBuilder.UseConnectionFactory(new NamedPipeConnectionFactory());
    }
}
