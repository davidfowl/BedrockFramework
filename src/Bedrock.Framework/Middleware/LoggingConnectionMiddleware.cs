// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO.Pipelines;
using System.Threading.Tasks;
using Bedrock.Framework.Infrastructure;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace Bedrock.Framework;

internal class LoggingConnectionMiddleware(ConnectionDelegate next, ILogger logger, LoggingFormatter loggingFormatter = null)
{
    private readonly ConnectionDelegate _next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task OnConnectionAsync(ConnectionContext context)
    {
        var oldTransport = context.Transport;

        try
        {
            await using var loggingDuplexPipe = new LoggingDuplexPipe(context.Transport, _logger, loggingFormatter);

            context.Transport = loggingDuplexPipe;

            await _next(context).ConfigureAwait(false);
        }
        finally
        {
            context.Transport = oldTransport;
        }
    }

    private class LoggingDuplexPipe(IDuplexPipe transport, ILogger logger, LoggingFormatter loggingFormatter) : 
        DuplexPipeStreamAdapter<LoggingStream>(transport, stream => new LoggingStream(stream, logger, loggingFormatter))
    {
    }
}
