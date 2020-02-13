// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO.Pipelines;
using System.Threading.Tasks;
using Bedrock.Framework.Infrastructure;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace Bedrock.Framework
{
    internal class LoggingConnectionMiddleware
    {
        private readonly ConnectionDelegate _next;
        private readonly ILogger _logger;
        private readonly LoggingFormatter _loggingFormatter;

        public LoggingConnectionMiddleware(ConnectionDelegate next, ILogger logger, LoggingFormatter loggingFormatter = null)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _loggingFormatter = loggingFormatter;
        }

        public async Task OnConnectionAsync(ConnectionContext context)
        {
            var oldTransport = context.Transport;

            try
            {
                await using (var loggingDuplexPipe = new LoggingDuplexPipe(context.Transport, _logger, _loggingFormatter))
                {
                    context.Transport = loggingDuplexPipe;

                    await _next(context).ConfigureAwait(false);
                }
            }
            finally
            {
                context.Transport = oldTransport;
            }
        }

        private class LoggingDuplexPipe : DuplexPipeStreamAdapter<LoggingStream>
        {
            public LoggingDuplexPipe(IDuplexPipe transport, ILogger logger, LoggingFormatter loggingFormatter) :
                base(transport, stream => new LoggingStream(stream, logger, loggingFormatter))
            {
            }
        }
    }
}
