// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO.Pipelines;
using System.Net.Connections;
using System.Threading.Tasks;
using Bedrock.Framework.Infrastructure;
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

        public async Task OnConnectionAsync(Connection connection)
        {
            await using var loggingDuplexPipe = new LoggingDuplexPipe(connection.Pipe, _logger, _loggingFormatter);

            var loggingConnection = Connection.FromPipe(loggingDuplexPipe, leaveOpen: true, connection.ConnectionProperties, connection.LocalEndPoint, connection.RemoteEndPoint); ;

            await _next(loggingConnection).ConfigureAwait(false);
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
