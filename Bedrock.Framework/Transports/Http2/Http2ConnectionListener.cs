using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
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
    public class Http2ConnectionListener : IConnectionListener, IHttpApplication<HttpContext>
    {
        private readonly KestrelServer _server;
        private readonly Channel<ConnectionContext> _acceptQueue = Channel.CreateUnbounded<ConnectionContext>(new UnboundedChannelOptions
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

        public async ValueTask UnbindAsync(CancellationToken cancellationToken = default)
        {
            await _server.StopAsync(cancellationToken);

            _acceptQueue.Writer.TryComplete();
        }

        private class HttpConnectionContext : ConnectionContext,
            IConnectionLifetimeFeature,
            IConnectionEndPointFeature,
            IConnectionItemsFeature,
            IConnectionIdFeature,
            IConnectionTransportFeature,
            IDuplexPipe
        {
            private readonly HttpContext _httpContext;
            private readonly TaskCompletionSource<object> _executionTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            public HttpConnectionContext(HttpContext httpContext)
            {
                _httpContext = httpContext;
                Transport = this;
                Items = httpContext.Items;
                Features = _httpContext.Features;
                ConnectionId = _httpContext.TraceIdentifier;
                LocalEndPoint = new IPEndPoint(httpContext.Connection.LocalIpAddress, httpContext.Connection.LocalPort);
                RemoteEndPoint = new IPEndPoint(httpContext.Connection.RemoteIpAddress, httpContext.Connection.RemotePort);
                ConnectionClosed = httpContext.RequestAborted;

                Features.Set<IConnectionIdFeature>(this);
                Features.Set<IConnectionTransportFeature>(this);
                Features.Set<IConnectionItemsFeature>(this);
                Features.Set<IConnectionEndPointFeature>(this);
                Features.Set<IConnectionLifetimeFeature>(this);
            }

            public Task ExecutionTask => _executionTcs.Task;

            public override string ConnectionId { get; set; }

            public override IFeatureCollection Features { get; }

            public override IDictionary<object, object> Items { get; set; }
            public override IDuplexPipe Transport { get; set; }

            public override EndPoint LocalEndPoint { get; set; }

            public override EndPoint RemoteEndPoint { get; set; }

            public PipeReader Input => _httpContext.Request.BodyReader;

            public PipeWriter Output => _httpContext.Response.BodyWriter;

            public override CancellationToken ConnectionClosed { get; set; }

            public override void Abort(ConnectionAbortedException abortReason)
            {
                _httpContext.Abort();
            }

            public override ValueTask DisposeAsync()
            {
                _executionTcs.TrySetResult(null);
                return base.DisposeAsync();
            }
        }
    }
}
