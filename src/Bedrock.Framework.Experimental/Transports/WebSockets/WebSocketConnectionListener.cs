using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Bedrock.Framework.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Bedrock.Framework
{
    public class WebSocketConnectionListener : ConnectionListener, IConnectionProperties, IHttpApplication<HttpContext>
    {
        private readonly KestrelServer _server;
        private readonly Channel<Connection> _acceptQueue = Channel.CreateUnbounded<Connection>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        private readonly RequestDelegate _application;

        public WebSocketConnectionListener(KestrelServer server, Action<Microsoft.AspNetCore.Http.Connections.WebSocketOptions> configure, IServiceProvider serviceProvider, string path)
        {
            _server = server;
            var builder = new ApplicationBuilder(serviceProvider);

            builder.UseRouting();
            builder.UseEndpoints(routes =>
            {
                var options = new HttpConnectionDispatcherOptions();
                configure(options.WebSockets);
                routes.MapConnections(path, options, cb => cb.Run(inner =>
                {
                    var connection = new WebSocketConnection(inner);

                    _acceptQueue.Writer.TryWrite(connection);

                    return connection.ExecutionTask;
                }));
            });

            _application = builder.Build();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return _server.StartAsync(this, cancellationToken);
        }

        public EndPoint EndPoint { get; set; }

        public override IConnectionProperties ListenerProperties => this;

        public override EndPoint LocalEndPoint => EndPoint;

        public override async ValueTask<Connection> AcceptAsync(IConnectionProperties options = null, CancellationToken cancellationToken = default)
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

        public HttpContext CreateContext(IFeatureCollection contextFeatures)
        {
            return new DefaultHttpContext(contextFeatures);
        }

        public void DisposeContext(HttpContext context, Exception exception)
        {

        }

        public Task ProcessRequestAsync(HttpContext context)
        {
            return _application(context);
        }

        protected override async ValueTask DisposeAsyncCore()
        {
            await _server.StopAsync(default).ConfigureAwait(false);

            _acceptQueue.Writer.TryComplete();

            await base.DisposeAsyncCore();
        }

        public bool TryGet(Type propertyKey, [NotNullWhen(true)] out object property)
        {
            property = _server.Features[propertyKey];
            return property != null;
        }

        // This exists solely to track the lifetime of the connection
        private class WebSocketConnection : Connection, IConnectionProperties
        {
            private readonly ConnectionContext _connection;
            private readonly TaskCompletionSource<object> _executionTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            public WebSocketConnection(ConnectionContext connection)
            {
                _connection = connection;
            }

            public Task ExecutionTask => _executionTcs.Task;

            protected override IDuplexPipe CreatePipe() => _connection.Transport;

            protected override Stream CreateStream() => new DuplexPipeStream(_connection.Transport.Input, _connection.Transport.Output);

            public override EndPoint LocalEndPoint => _connection.LocalEndPoint;

            public override EndPoint RemoteEndPoint => _connection.RemoteEndPoint;

            public override IConnectionProperties ConnectionProperties => this;

            protected override async ValueTask CloseAsyncCore(ConnectionCloseMethod method, CancellationToken cancellationToken)
            {
                _executionTcs.TrySetResult(null);

                switch (method)
                {
                    case ConnectionCloseMethod.Abort:
                        _connection.Abort();
                        break;
                    case ConnectionCloseMethod.Immediate:
                    case ConnectionCloseMethod.GracefulShutdown:
                        await _connection.DisposeAsync();
                        break;
                    default:
                        break;
                }
            }

            public bool TryGet(Type propertyKey, [NotNullWhen(true)] out object property)
            {
                property = _connection.Features[propertyKey];
                return property != null;
            }
        }
    }
}
