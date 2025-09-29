using System;
using System.IO.Pipelines;
using Microsoft.AspNetCore.Connections;

#nullable enable

namespace Bedrock.Framework.Experimental.Tests.Infrastructure
{
    internal static class ConnectionsHelper
    {
        public static DefaultConnectionContext CreateNewConnectionContext(PipeOptions? options = default)
        {
            options ??= new PipeOptions(useSynchronizationContext: false);
            var pair = DuplexPipe.CreateConnectionPair(options, options);

            return new DefaultConnectionContext(Guid.NewGuid().ToString(), pair.Transport, pair.Application);
        }
    }
}