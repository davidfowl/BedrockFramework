using System;
using System.Collections.Generic;
using System.Text;
using Bedrock.Framework.Infrastructure;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bedrock.Framework
{
    public delegate void LoggingFormatter(ILogger logger, string method, ReadOnlySpan<byte> buffer);

    public static class ConnectionBuilderExtensions
    {
        /// <summary>
        /// Emits verbose logs for bytes read from and written to the connection.
        /// </summary>
        public static TBuilder UseConnectionLogging<TBuilder>(this TBuilder builder, string? loggerName = null, ILoggerFactory? loggerFactory = null, LoggingFormatter? loggingFormatter = null) where TBuilder : IConnectionBuilder
        {
            loggerFactory ??= builder.ApplicationServices.GetRequiredService<ILoggerFactory>();
            var logger = loggerName == null ? loggerFactory.CreateLogger<LoggingConnectionMiddleware>() : loggerFactory.CreateLogger(loggerName);
            builder.Use(next => new LoggingConnectionMiddleware(next, logger, loggingFormatter).OnConnectionAsync);
            return builder;
        }

        public static TBuilder UseConnectionLogging<TBuilder>(this TBuilder builder, ILogger logger, LoggingFormatter? loggingFormatter = null) where TBuilder : IConnectionBuilder
        {
            builder.Use(next => new LoggingConnectionMiddleware(next, logger, loggingFormatter).OnConnectionAsync);
            return builder;
        }

        public static TBuilder UseConnectionLimits<TBuilder>(this TBuilder builder, int connectionLimit) where TBuilder : IConnectionBuilder
        {
            var loggerFactory = builder.ApplicationServices.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
            var logger = loggerFactory.CreateLogger<ConnectionLimitMiddleware>();
            builder.Use(next => new ConnectionLimitMiddleware(next, logger, connectionLimit).OnConnectionAsync);
            return builder;
        }
    }

}
