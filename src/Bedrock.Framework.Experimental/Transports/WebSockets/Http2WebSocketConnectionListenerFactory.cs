using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Bedrock.Framework.Experimental.Protocols.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Bedrock.Framework
{
    /// <summary>
    /// Options for <see cref="Http2WebSocketConnectionListenerFactory"/>.
    /// </summary>
    public sealed class Http2WebSocketListenerOptions
    {
        /// <summary>
        /// The server certificate. Required for wss:// / https:// endpoints unless a development certificate is installed.
        /// </summary>
        public X509Certificate2 Certificate { get; set; }

        /// <summary>
        /// Configures Kestrel's TLS for the endpoint: certificate selection (SNI), client certificates, SSL protocols,
        /// handshake timeout, OnAuthenticate and so on. Applied after <see cref="Certificate"/> and Kestrel's HTTPS defaults.
        /// </summary>
        public Action<Microsoft.AspNetCore.Server.Kestrel.Https.HttpsConnectionAdapterOptions> ConfigureHttps { get; set; }

        /// <summary>
        /// The HTTP protocols to accept. Defaults to HTTP/1.1 and HTTP/2 (negotiated by ALPN) with TLS, and HTTP/2 only
        /// (prior knowledge, h2c) without TLS.
        /// </summary>
        public HttpProtocols? Protocols { get; set; }

        /// <summary>
        /// Additional Kestrel configuration.
        /// </summary>
        public Action<KestrelServerOptions> ConfigureKestrel { get; set; }
    }

    /// <summary>
    /// Hosts WebSocket endpoints on Kestrel, accepting RFC 8441 extended CONNECT over HTTP/2 as well as RFC 6455
    /// upgrades over HTTP/1.1. Each WebSocket request is surfaced as a connection carrying an
    /// <see cref="IWebSocketUpgradeFeature"/>; complete the handshake with <see cref="WebSocketServerProtocol"/>,
    /// <c>RunWebSocket</c> or <c>UseWebSocketServer</c>, exactly as for a socket connection.
    /// </summary>
    public sealed class Http2WebSocketConnectionListenerFactory : IConnectionListenerFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly Http2WebSocketListenerOptions _options;

        public Http2WebSocketConnectionListenerFactory(ILoggerFactory loggerFactory, Http2WebSocketListenerOptions options = null)
        {
            _loggerFactory = loggerFactory;
            _options = options ?? new Http2WebSocketListenerOptions();
        }

        public async ValueTask<IConnectionListener> BindAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
        {
            IPEndPoint ipEndPoint;
            var useTls = _options.Certificate != null || _options.ConfigureHttps != null;
            switch (endpoint)
            {
                case UriEndPoint uriEndPoint:
                    var uri = uriEndPoint.Uri;
                    var address = uri.IsLoopback && uri.Host == "localhost" ? IPAddress.Loopback : IPAddress.Parse(uri.Host);
                    var port = uri.IsDefaultPort && uri.Scheme is "ws" or "wss" ? (uri.Scheme == "wss" ? 443 : 80) : uri.Port;
                    ipEndPoint = new IPEndPoint(address, port);
                    useTls = uri.Scheme is "wss" or "https";
                    break;
                case IPEndPoint ip:
                    ipEndPoint = ip;
                    break;
                default:
                    throw new NotSupportedException($"{endpoint} is not supported.");
            }

            // Host Kestrel through a slim WebApplication so HTTPS (and ALPN for HTTP/2) can be configured; since .NET 8
            // a hand-constructed KestrelServer cannot call UseHttps.
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton(_loggerFactory);
            builder.WebHost.UseKestrelHttpsConfiguration();

            ListenOptions listenOptions = null;
            builder.WebHost.ConfigureKestrel(kestrel =>
            {
                _options.ConfigureKestrel?.Invoke(kestrel);
                kestrel.Listen(ipEndPoint, options =>
                {
                    if (useTls)
                    {
                        options.UseHttps(https =>
                        {
                            if (_options.Certificate != null)
                            {
                                https.ServerCertificate = _options.Certificate;
                            }
                            _options.ConfigureHttps?.Invoke(https);
                        });
                    }

                    options.Protocols = _options.Protocols ?? (useTls ? HttpProtocols.Http1AndHttp2 : HttpProtocols.Http2);
                    listenOptions = options;
                });
            });

            var app = builder.Build();
            var listener = new Http2WebSocketConnectionListener(app);
            await listener.BindAsync(cancellationToken).ConfigureAwait(false);
            listener.EndPoint = listenOptions.IPEndPoint;
            return listener;
        }
    }

    internal sealed class Http2WebSocketConnectionListener : IConnectionListener
    {
        private readonly WebApplication _app;
        private readonly Channel<ConnectionContext> _acceptQueue = Channel.CreateUnbounded<ConnectionContext>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        public Http2WebSocketConnectionListener(WebApplication app)
        {
            _app = app;
            _app.Run(ProcessRequestAsync);
        }

        public EndPoint EndPoint { get; set; }

        public Task BindAsync(CancellationToken cancellationToken) => _app.StartAsync(cancellationToken);

        public async ValueTask<ConnectionContext> AcceptAsync(CancellationToken cancellationToken = default)
        {
            while (await _acceptQueue.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_acceptQueue.Reader.TryRead(out var connection))
                {
                    return connection;
                }
            }
            return null;
        }

        public async Task ProcessRequestAsync(HttpContext context)
        {
            var extendedConnect = context.Features.Get<IHttpExtendedConnectFeature>();
            var upgrade = context.Features.Get<IHttpUpgradeFeature>();

            var isExtendedConnect = extendedConnect?.IsExtendedConnect == true;
            if (!isExtendedConnect && upgrade?.IsUpgradableRequest != true)
            {
                // Not a WebSocket request of either kind
                context.Response.StatusCode = HttpMethods.IsConnect(context.Request.Method) ? StatusCodes.Status501NotImplemented : StatusCodes.Status426UpgradeRequired;
                context.Response.Headers[WebSocketHandshake.HeaderVersion] = WebSocketHandshake.SupportedVersion;
                return;
            }

            // WebSocket tunnels are long-lived streams: no body size limit or minimum data rate
            if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } maxBody)
            {
                maxBody.MaxRequestBodySize = null;
            }
            if (context.Features.Get<IHttpMinRequestBodyDataRateFeature>() is { } minRate)
            {
                minRate.MinDataRate = null;
            }

            var connection = new HttpWebSocketConnectionContext(context, extendedConnect, upgrade, isExtendedConnect);
            _acceptQueue.Writer.TryWrite(connection);
            await connection.ExecutionTask.ConfigureAwait(false);
        }

        public async ValueTask UnbindAsync(CancellationToken cancellationToken = default)
        {
            await _app.StopAsync(cancellationToken).ConfigureAwait(false);
            _acceptQueue.Writer.TryComplete();
        }

        public async ValueTask DisposeAsync()
        {
            await UnbindAsync().ConfigureAwait(false);
            await _app.DisposeAsync().ConfigureAwait(false);
        }

        private sealed class HttpWebSocketConnectionContext : ConnectionContext, IWebSocketUpgradeFeature, IConnectionLifetimeFeature, IConnectionIdFeature, IConnectionItemsFeature, IConnectionEndPointFeature
        {
            private readonly HttpContext _httpContext;
            private readonly IHttpExtendedConnectFeature _extendedConnect;
            private readonly IHttpUpgradeFeature _upgrade;
            private readonly TaskCompletionSource _executionTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private StreamDuplexPipe _tunnel;

            public HttpWebSocketConnectionContext(HttpContext httpContext, IHttpExtendedConnectFeature extendedConnect, IHttpUpgradeFeature upgrade, bool isExtendedConnect)
            {
                _httpContext = httpContext;
                _extendedConnect = extendedConnect;
                _upgrade = upgrade;
                IsExtendedConnect = isExtendedConnect;

                Features = httpContext.Features;
                Items = httpContext.Items;
                ConnectionId = httpContext.TraceIdentifier;
                LocalEndPoint = httpContext.Connection.LocalIpAddress is { } local ? new IPEndPoint(local, httpContext.Connection.LocalPort) : null;
                RemoteEndPoint = httpContext.Connection.RemoteIpAddress is { } remote ? new IPEndPoint(remote, httpContext.Connection.RemotePort) : null;
                ConnectionClosed = httpContext.RequestAborted;

                // Until the upgrade is accepted, the transport is the request/response body
                Transport = new BodyPipe(httpContext.Request.BodyReader, httpContext.Response.BodyWriter);

                var headers = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
                foreach (var header in httpContext.Request.Headers)
                {
                    headers[header.Key] = header.Value;
                }
                if (!headers.ContainsKey(WebSocketHandshake.HeaderHost) && httpContext.Request.Host.HasValue)
                {
                    headers[WebSocketHandshake.HeaderHost] = httpContext.Request.Host.Value;
                }
                Headers = headers;

                var requestFeature = httpContext.Features.Get<IHttpRequestFeature>();
                RequestTarget = string.IsNullOrEmpty(requestFeature?.RawTarget)
                    ? httpContext.Request.PathBase + httpContext.Request.Path + httpContext.Request.QueryString
                    : requestFeature.RawTarget;

                Features.Set<IWebSocketUpgradeFeature>(this);
                Features.Set<IConnectionLifetimeFeature>(this);
                Features.Set<IConnectionIdFeature>(this);
                Features.Set<IConnectionItemsFeature>(this);
                Features.Set<IConnectionEndPointFeature>(this);
            }

            public Task ExecutionTask => _executionTcs.Task;

            public override string ConnectionId { get; set; }
            public override IFeatureCollection Features { get; }
            public override IDictionary<object, object> Items { get; set; }
            public override IDuplexPipe Transport { get; set; }
            public override EndPoint LocalEndPoint { get; set; }
            public override EndPoint RemoteEndPoint { get; set; }
            public override CancellationToken ConnectionClosed { get; set; }

            // IWebSocketUpgradeFeature
            public bool IsExtendedConnect { get; }
            public string Method => _httpContext.Request.Method;
            public string RequestTarget { get; }
            public string HttpVersion => _httpContext.Request.Protocol;
            public string Protocol => _extendedConnect?.Protocol;
            public IReadOnlyDictionary<string, StringValues> Headers { get; }

            public async ValueTask<IDuplexPipe> AcceptAsync(IReadOnlyDictionary<string, string> responseHeaders, CancellationToken cancellationToken)
            {
                foreach (var (name, value) in responseHeaders)
                {
                    _httpContext.Response.Headers[name] = value;
                }

                System.IO.Stream stream;
                if (IsExtendedConnect)
                {
                    // Sends 200 and returns the bidirectional HTTP/2 stream
                    stream = await _extendedConnect.AcceptAsync().ConfigureAwait(false);
                }
                else
                {
                    // Sends 101 Switching Protocols with Connection: Upgrade
                    _httpContext.Response.Headers[WebSocketHandshake.HeaderUpgrade] = "websocket";
                    stream = await _upgrade.UpgradeAsync().ConfigureAwait(false);
                }

                _tunnel = new StreamDuplexPipe(stream);
                Transport = _tunnel;
                return _tunnel;
            }

            public async ValueTask RejectAsync(int statusCode, IReadOnlyDictionary<string, string> responseHeaders, CancellationToken cancellationToken)
            {
                _httpContext.Response.StatusCode = statusCode;
                foreach (var (name, value) in responseHeaders)
                {
                    _httpContext.Response.Headers[name] = value;
                }
                await _httpContext.Response.CompleteAsync().ConfigureAwait(false);
            }

            public override void Abort(ConnectionAbortedException abortReason) => _httpContext.Abort();

            private sealed class BodyPipe(PipeReader input, PipeWriter output) : IDuplexPipe
            {
                public PipeReader Input { get; } = input;
                public PipeWriter Output { get; } = output;
            }

            public override async ValueTask DisposeAsync()
            {
                if (_tunnel != null)
                {
                    await _tunnel.DisposeAsync().ConfigureAwait(false);
                }
                _executionTcs.TrySetResult();
            }
        }
    }

    public static class Http2WebSocketServerBuilderExtensions
    {
        /// <summary>
        /// Listens for WebSocket requests on Kestrel: RFC 8441 extended CONNECT over HTTP/2 (h2c for ws://, ALPN over TLS
        /// for wss://) and RFC 6455 upgrades over HTTP/1.1 where enabled. Complete the handshake in the pipeline with
        /// <c>RunWebSocket</c>, <c>UseWebSocketServer</c> or <see cref="WebSocketServerProtocol"/>.
        /// </summary>
        public static ServerBuilder ListenHttp2WebSocket(this ServerBuilder builder, EndPoint endPoint, Action<IConnectionBuilder> configure, Action<Http2WebSocketListenerOptions> configureListener = null)
        {
            var options = new Http2WebSocketListenerOptions();
            configureListener?.Invoke(options);

            var loggerFactory = builder.ApplicationServices.GetService<ILoggerFactory>() ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
            return builder.Listen(endPoint, new Http2WebSocketConnectionListenerFactory(loggerFactory, options), configure);
        }
    }
}
