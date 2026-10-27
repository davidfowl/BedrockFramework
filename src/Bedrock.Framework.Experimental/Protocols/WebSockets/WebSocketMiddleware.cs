using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Bedrock.Framework.Experimental.Protocols.WebSockets;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bedrock.Framework.Experimental.Protocols.WebSockets
{
    /// <summary>
    /// Gives middleware and handlers that run over a WebSocket access to the session.
    /// </summary>
    public interface IWebSocketSessionFeature
    {
        WebSocketSession Session { get; }

        /// <summary>
        /// The upgrade request, on the server. Null on the client.
        /// </summary>
        WebSocketUpgradeRequest Request { get; }
    }

    internal sealed class WebSocketSessionFeature(WebSocketSession session, WebSocketUpgradeRequest request) : IWebSocketSessionFeature
    {
        public WebSocketSession Session { get; } = session;
        public WebSocketUpgradeRequest Request { get; } = request;
    }

    /// <summary>
    /// Convenience extensions for performing the WebSocket handshake on a Bedrock connection.
    /// </summary>
    public static class WebSocketConnectionExtensions
    {
        /// <summary>
        /// Reads the upgrade request from the connection and accepts it.
        /// </summary>
        public static ValueTask<WebSocketSession> AcceptWebSocketAsync(this ConnectionContext connection, WebSocketServerOptions options = null, CancellationToken cancellationToken = default)
            => WebSocketServerProtocol.UpgradeAsync(connection, options, cancellationToken);

        /// <summary>
        /// Performs the client handshake over the connection.
        /// </summary>
        public static ValueTask<WebSocketSession> ConnectWebSocketAsync(this ConnectionContext connection, Uri uri, WebSocketClientOptions options = null, CancellationToken cancellationToken = default)
            => WebSocketClientProtocol.ConnectAsync(connection, uri, options, cancellationToken);

        /// <summary>
        /// Adapts the session to a byte-stream duplex pipe. See <see cref="WebSocketDuplexPipe"/>.
        /// </summary>
        public static WebSocketDuplexPipe AsDuplexPipe(this WebSocketSession session, WebSocketMessageType messageType = WebSocketMessageType.Binary)
            => new(session, messageType);
    }

    internal sealed class WebSocketServerMiddleware(ConnectionDelegate next, WebSocketServerOptions options, string path, WebSocketMessageType messageType, ILogger logger)
    {
        public async Task OnConnectionAsync(ConnectionContext connection)
        {
            var handshake = new WebSocketServerProtocol(connection, options);
            WebSocketUpgradeRequest request;
            try
            {
                request = await handshake.ReadRequestAsync(connection.ConnectionClosed).ConfigureAwait(false);
            }
            catch (WebSocketHandshakeException ex)
            {
                logger.LogDebug(ex, "Rejected WebSocket upgrade on connection {ConnectionId}.", connection.ConnectionId);
                return;
            }

            if (path != null && !string.Equals(request.Path, path, StringComparison.Ordinal))
            {
                await handshake.RejectAsync(404).ConfigureAwait(false);
                return;
            }

            var session = await handshake.AcceptAsync(cancellationToken: connection.ConnectionClosed).ConfigureAwait(false);
            var originalTransport = connection.Transport;
            await using var pipe = new WebSocketDuplexPipe(session, messageType);

            connection.Transport = pipe;
            connection.Features.Set<IWebSocketSessionFeature>(new WebSocketSessionFeature(session, request));
            try
            {
                await next(connection).ConfigureAwait(false);
            }
            finally
            {
                connection.Transport = originalTransport;
            }
        }
    }

    internal sealed class WebSocketClientMiddleware(ConnectionDelegate next, Uri uri, WebSocketClientOptions options, WebSocketMessageType messageType)
    {
        public async Task OnConnectionAsync(ConnectionContext connection)
        {
            var session = await WebSocketClientProtocol.ConnectAsync(connection, uri, options, connection.ConnectionClosed).ConfigureAwait(false);
            var originalTransport = connection.Transport;
            await using var pipe = new WebSocketDuplexPipe(session, messageType);

            connection.Transport = pipe;
            connection.Features.Set<IWebSocketSessionFeature>(new WebSocketSessionFeature(session, null));
            try
            {
                await next(connection).ConfigureAwait(false);
            }
            finally
            {
                connection.Transport = originalTransport;
            }
        }
    }
}

namespace Bedrock.Framework
{
    public static class WebSocketBuilderExtensions
    {
        /// <summary>
        /// Upgrades incoming HTTP/1.1 connections to WebSockets and runs the rest of the pipeline over the WebSocket:
        /// downstream middleware sees a byte stream of message payloads as its transport, and flushed output is sent as
        /// messages (message boundaries are not preserved, see <see cref="WebSocketDuplexPipe"/>).
        /// The session is available through <see cref="IWebSocketSessionFeature"/>. Use after TLS middleware for wss://.
        /// </summary>
        /// <param name="builder">The connection builder.</param>
        /// <param name="configure">Configures server options.</param>
        /// <param name="path">When set, only upgrade requests for this path are accepted; others receive 404.</param>
        /// <param name="messageType">The message type used for outgoing data.</param>
        public static TBuilder UseWebSocketServer<TBuilder>(this TBuilder builder, Action<WebSocketServerOptions> configure = null, string path = null, WebSocketMessageType messageType = WebSocketMessageType.Binary)
            where TBuilder : IConnectionBuilder
        {
            var options = new WebSocketServerOptions();
            configure?.Invoke(options);

            var loggerFactory = builder.ApplicationServices.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
            var logger = loggerFactory.CreateLogger<WebSocketServerMiddleware>();
            builder.Use(next => new WebSocketServerMiddleware(next, options, path, messageType, logger).OnConnectionAsync);
            return builder;
        }

        /// <summary>
        /// Performs the WebSocket client handshake on outgoing connections and runs the rest of the pipeline over the
        /// WebSocket. Use after TLS middleware for wss://.
        /// </summary>
        public static ClientBuilder UseWebSocketClient(this ClientBuilder builder, Uri uri, Action<WebSocketClientOptions> configure = null, WebSocketMessageType messageType = WebSocketMessageType.Binary)
        {
            ArgumentNullException.ThrowIfNull(uri);
            var options = new WebSocketClientOptions();
            configure?.Invoke(options);

            builder.Use(next => new WebSocketClientMiddleware(next, uri, options, messageType).OnConnectionAsync);
            return builder;
        }

        /// <summary>
        /// Terminates the pipeline with a WebSocket application that works with the session directly.
        /// </summary>
        public static TBuilder RunWebSocket<TBuilder>(this TBuilder builder, Func<WebSocketSession, WebSocketUpgradeRequest, Task> application, Action<WebSocketServerOptions> configure = null)
            where TBuilder : IConnectionBuilder
        {
            ArgumentNullException.ThrowIfNull(application);
            var options = new WebSocketServerOptions();
            configure?.Invoke(options);

            var loggerFactory = builder.ApplicationServices.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
            var logger = loggerFactory.CreateLogger("Bedrock.Framework.WebSockets");

            builder.Run(async connection =>
            {
                var handshake = new WebSocketServerProtocol(connection, options);
                WebSocketUpgradeRequest request;
                try
                {
                    request = await handshake.ReadRequestAsync(connection.ConnectionClosed).ConfigureAwait(false);
                }
                catch (WebSocketHandshakeException ex)
                {
                    logger.LogDebug(ex, "Rejected WebSocket upgrade on connection {ConnectionId}.", connection.ConnectionId);
                    return;
                }

                await using var session = await handshake.AcceptAsync(cancellationToken: connection.ConnectionClosed).ConfigureAwait(false);
                await application(session, request).ConfigureAwait(false);

                if (session.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    await session.CloseAsync(WebSocketCloseStatus.NormalClosure).ConfigureAwait(false);
                }
            });
            return builder;
        }
    }
}
