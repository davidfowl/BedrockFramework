using System;
using System.Collections.Generic;
using System.Text;
using Bedrock.Framework.Middleware.Tls;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bedrock.Framework
{
    public static class ConnectionBuilderExtensions
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

        public static TBuilder UseConnectionLimits<TBuilder>(this TBuilder builder, int connectionLimit) where TBuilder : IConnectionBuilder
        {
            var loggerFactory = builder.ApplicationServices.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
            var logger = loggerFactory.CreateLogger<ConnectionLimitMiddleware>();
            builder.Use(next => new ConnectionLimitMiddleware(next, logger, connectionLimit).OnConnectionAsync);
            return builder;
        }

        public static TBuilder UseServerTls<TBuilder>(
            this TBuilder builder,
            Action<TlsOptions> configure) where TBuilder : IConnectionBuilder
        {
            var options = new TlsOptions();
            configure(options);
            return builder.UseServerTls(options);
        }

        public static TBuilder UseServerTls<TBuilder>(
            this TBuilder builder,
            TlsOptions options) where TBuilder : IConnectionBuilder
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var loggerFactory = builder.ApplicationServices.GetService(typeof(ILoggerFactory)) as ILoggerFactory ?? NullLoggerFactory.Instance;
            builder.Use(next =>
            {
                var middleware = new TlsServerConnectionMiddleware(next, options, loggerFactory);
                return middleware.OnConnectionAsync;
            });
            return builder;
        }

        public static TBuilder UseClientTls<TBuilder>(
            this TBuilder builder,
            Action<TlsOptions> configure) where TBuilder : IConnectionBuilder
        {
            var options = new TlsOptions();
            configure(options);
            return builder.UseClientTls(options);
        }

        public static TBuilder UseClientTls<TBuilder>(
            this TBuilder builder,
            TlsOptions options) where TBuilder : IConnectionBuilder
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var loggerFactory = builder.ApplicationServices.GetService(typeof(ILoggerFactory)) as ILoggerFactory ?? NullLoggerFactory.Instance;
            builder.Use(next =>
            {
                var middleware = new TlsClientConnectionMiddleware(next, options, loggerFactory);
                return middleware.OnConnectionAsync;
            });
            return builder;
        }
    }

}
