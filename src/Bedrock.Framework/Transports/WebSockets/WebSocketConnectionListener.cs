using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Bedrock.Framework
{
    public class WebSocketConnectionListener : IConnectionListener, IHttpApplication<HttpContext>
    {
        private readonly KestrelServer _server;
        private readonly Channel<ConnectionContext> _acceptQueue = Channel.CreateUnbounded<ConnectionContext>(new UnboundedChannelOptions
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
                    var connection = new WebSocketConnectionContext(inner);

                    _acceptQueue.Writer.TryWrite(connection);

                    return connection.ExecutionTask;
                }));
            });

            _application = builder.Build();
        }

        public Task BindAsync(CancellationToken cancellationToken)
        {
            return _server.StartAsync(this, cancellationToken);
        }

        public EndPoint EndPoint { get; set; }

        public async ValueTask<ConnectionContext> AcceptAsync(CancellationToken cancellationToken = default)
        {
            while (await _acceptQueue.Reader.WaitToReadAsync(cancellationToken))
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

        public async ValueTask DisposeAsync()
        {
            await UnbindAsync();

            _server.Dispose();
        }

        public void DisposeContext(HttpContext context, Exception exception)
        {

        }

        public Task ProcessRequestAsync(HttpContext context)
        {
            return _application(context);
        }

        public async ValueTask UnbindAsync(CancellationToken cancellationToken = default)
        {
            await _server.StopAsync(cancellationToken);

            _acceptQueue.Writer.TryComplete();
        }

        // This exists solely to track the lifetime of the connection
        private class WebSocketConnectionContext : ConnectionContext
        {
            private readonly ConnectionContext _connection;
            private readonly TaskCompletionSource<object> _executionTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            public WebSocketConnectionContext(ConnectionContext connection)
            {
                _connection = connection;
            }

            public Task ExecutionTask => _executionTcs.Task;

            public override string ConnectionId
            {
                get => _connection.ConnectionId;
                set => _connection.ConnectionId = value;
            }

            public override IFeatureCollection Features => _connection.Features;

            public override IDictionary<object, object> Items
            {
                get => _connection.Items;
                set => _connection.Items = value;
            }

            public override IDuplexPipe Transport
            {
                get => _connection.Transport;
                set => _connection.Transport = value;
            }

            public override EndPoint LocalEndPoint
            {
                get => _connection.LocalEndPoint;
                set => _connection.LocalEndPoint = value;
            }

            public override EndPoint RemoteEndPoint
            {
                get => _connection.RemoteEndPoint;
                set => _connection.RemoteEndPoint = value;
            }

            public override CancellationToken ConnectionClosed
            {
                get => _connection.ConnectionClosed;
                set => _connection.ConnectionClosed = value;
            }

            public override void Abort()
            {
                _connection.Abort();
            }

            public override void Abort(ConnectionAbortedException abortReason)
            {
                _connection.Abort(abortReason);
            }

            public override ValueTask DisposeAsync()
            {
                _executionTcs.TrySetResult(null);
                return _connection.DisposeAsync();
            }
        }
    }
}
