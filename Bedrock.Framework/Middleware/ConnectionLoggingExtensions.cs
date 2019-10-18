using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bedrock.Framework
{
    public static class ConnectionLoggingExtensions
    {
        /// <summary>
        /// Emits verbose logs for bytes read from and written to the connection.
        /// </summary>
        /// <returns>
        /// The <see cref="ListenOptions"/>.
        /// </returns>
        public static TBuilder UseConnectionLogging<TBuilder>(this TBuilder builder) where TBuilder : IConnectionBuilder
        {
            return builder.UseConnectionLogging(loggerName: null);
        }

        /// <summary>
        /// Emits verbose logs for bytes read from and written to the connection.
        /// </summary>
        /// <returns>
        /// The <see cref="ListenOptions"/>.
        /// </returns>
        public static TBuilder UseConnectionLogging<TBuilder>(this TBuilder builder, string loggerName) where TBuilder : IConnectionBuilder
        {
            var loggerFactory = builder.ApplicationServices.GetRequiredService<ILoggerFactory>();
            var logger = loggerName == null ? loggerFactory.CreateLogger<LoggingConnectionMiddleware>() : loggerFactory.CreateLogger(loggerName);
            builder.Use(next => new LoggingConnectionMiddleware(next, logger).OnConnectionAsync);
            return builder;
        }
    }

}
