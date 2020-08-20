using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;

namespace Bedrock.Framework
{
    public class Http2ConnectionListener : ConnectionListener, IHttpApplication<HttpContext>, IConnectionProperties
    {
        private readonly KestrelServer _server;
        private readonly Channel<Connection> _acceptQueue = Channel.CreateUnbounded<Connection>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        public Http2ConnectionListener(KestrelServer server)
        {
            _server = server;
        }

        public Task BindAsync(CancellationToken cancellationToken)
        {
            return _server.StartAsync(this, cancellationToken);
        }

        public EndPoint EndPoint { get; set; }

        public override IConnectionProperties ListenerProperties => this;

        public override EndPoint LocalEndPoint => null;

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

        public async Task ProcessRequestAsync(HttpContext context)
        {
            // We're streaming here so there's no max body size nor is there a min data rate
            context.Features.Get<IHttpMaxRequestBodySizeFeature>().MaxRequestBodySize = null;
            context.Features.Get<IHttpMinRequestBodyDataRateFeature>().MinDataRate = null;

            // Flush the headers so that the client can start sending
            await context.Response.Body.FlushAsync();

            var httpConnectionContext = new HttpConnectionContext(context);
            _acceptQueue.Writer.TryWrite(httpConnectionContext);
            await httpConnectionContext.ExecutionTask;
        }

        protected override ValueTask DisposeAsyncCore()
        {
            _server.Dispose();

            return base.DisposeAsyncCore();
        }

        public async ValueTask UnbindAsync(CancellationToken cancellationToken = default)
        {
            await _server.StopAsync(cancellationToken).ConfigureAwait(false);

            _acceptQueue.Writer.TryComplete();
        }

        public bool TryGet(Type propertyKey, [NotNullWhen(true)] out object property)
        {
            property = _server.Features[propertyKey];
            return property != null;
        }

        private class HttpConnectionContext : Connection, IDuplexPipe, IConnectionProperties
        {
            private readonly HttpContext _httpContext;
            private readonly TaskCompletionSource<object> _executionTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            public HttpConnectionContext(HttpContext httpContext)
            {
                _httpContext = httpContext;
                Transport = this;
                LocalEndPoint = new IPEndPoint(httpContext.Connection.LocalIpAddress, httpContext.Connection.LocalPort);
                RemoteEndPoint = new IPEndPoint(httpContext.Connection.RemoteIpAddress, httpContext.Connection.RemotePort);
            }

            public Task ExecutionTask => _executionTcs.Task;

            public IDuplexPipe Transport { get; }

            public override IConnectionProperties ConnectionProperties => this;

            public override EndPoint LocalEndPoint { get; }

            public override EndPoint RemoteEndPoint { get; }

            public PipeReader Input => _httpContext.Request.BodyReader;

            public PipeWriter Output => _httpContext.Response.BodyWriter;

            public bool TryGet(Type propertyKey, [NotNullWhen(true)] out object property)
            {
                property = _httpContext.Features[propertyKey];
                return property != null;
            }

            protected override ValueTask CloseAsyncCore(ConnectionCloseMethod method, CancellationToken cancellationToken)
            {
                _executionTcs.TrySetResult(null);
                return default;
            }
        }
    }
}