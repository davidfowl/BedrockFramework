using System;
using System.Collections.Generic;
using System.Text;
using Bedrock.Framework.Infrastructure;
using Bedrock.Framework.Middleware.Tls;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bedrock.Framework
{
    public delegate void LoggingFormatter(ILogger logger, string method, ReadOnlySpan<byte> buffer);

    public class LoggingFormatting
    {
        public static void Wireshark(ILogger logger, string method, ReadOnlySpan<byte> buffer)
        {
            if (!logger.IsEnabled(LogLevel.Debug))
            {
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine($"{method}[{buffer.Length}]");
            var charBuilder = new StringBuilder();

            // Write the hex
            for (int i = 0; i < buffer.Length; i++)
            {
                builder.Append(buffer[i].ToString("X2"));
                builder.Append(" ");

                var bufferChar = (char)buffer[i];
                if (Char.IsControl(bufferChar))
                {
                    charBuilder.Append(".");
                }
                else
                {
                    charBuilder.Append(bufferChar);
                }

                if ((i + 1) % 16 == 0)
                {
                    builder.Append("  ");
                    builder.Append(charBuilder.ToString());
                    builder.AppendLine();
                    charBuilder.Clear();
                }
                else if ((i + 1) % 8 == 0)
                {
                    builder.Append(" ");
                    charBuilder.Append(" ");
                }
            }
            if (charBuilder.Length > 0)
            {
                // 2 (between hex and char blocks) + num bytes left (3 per byte)
                builder.Append(string.Empty.PadRight(2+(3 * (16 - charBuilder.Length))));
                // extra for space after 8th byte
                if (charBuilder.Length < 8)
                    builder.Append(" ");
                builder.Append(charBuilder.ToString());
            }

            logger.LogDebug(builder.ToString());
        }
    }

    public static class ConnectionBuilderExtensions
    {
        /// <summary>
        /// Emits verbose logs for bytes read from and written to the connection.
        /// </summary>
        /// <returns>
        /// The <see cref="ListenOptions"/>.
        /// </returns>
        public static TBuilder UseConnectionLogging<TBuilder>(this TBuilder builder, string loggerName = null, LoggingFormatter loggingFormatter = null) where TBuilder : IConnectionBuilder
        {
            var loggerFactory = builder.ApplicationServices.GetRequiredService<ILoggerFactory>();
            var logger = loggerName == null ? loggerFactory.CreateLogger<LoggingConnectionMiddleware>() : loggerFactory.CreateLogger(loggerName);
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
