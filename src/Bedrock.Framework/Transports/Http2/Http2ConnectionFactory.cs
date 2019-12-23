using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http.Features;

namespace Bedrock.Framework
{
    public class Http2ConnectionFactory : IConnectionFactory
    {
        private readonly HttpClient _client = new HttpClient();

        public async ValueTask<ConnectionContext> ConnectAsync(EndPoint endPoint, CancellationToken cancellationToken = default)
        {
            Uri uri = null;
            switch (endPoint)
            {
                case UriEndPoint uriEndPoint:
                    uri = uriEndPoint.Uri;
                    break;
                case IPEndPoint ip:
                    uri = new Uri($"https://{ip.Address}:{ip.Port}");
                    break;
                default:
                    throw new NotSupportedException($"{endPoint} not supported");
            }

            var request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Version = new Version(2, 0)
            };
            var connection = new HttpClientConnectionContext();
            request.Content = new HttpClientConnectionContextContent(connection);
            var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            connection.HttpResponseMessage = response;
            var responseStream = await response.Content.ReadAsStreamAsync();
            connection.Input = PipeReader.Create(responseStream);

            return connection;
        }

        private class HttpClientConnectionContext : ConnectionContext,
                IConnectionLifetimeFeature,
                IConnectionEndPointFeature,
                IConnectionItemsFeature,
                IConnectionIdFeature,
                IConnectionTransportFeature,
                IDuplexPipe
        {
            private readonly TaskCompletionSource<object> _executionTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            public HttpClientConnectionContext()
            {
                Transport = this;

                Features.Set<IConnectionIdFeature>(this);
                Features.Set<IConnectionTransportFeature>(this);
                Features.Set<IConnectionItemsFeature>(this);
                Features.Set<IConnectionEndPointFeature>(this);
                Features.Set<IConnectionLifetimeFeature>(this);
            }

            public Task ExecutionTask => _executionTcs.Task;

            public override string ConnectionId { get; set; } = Guid.NewGuid().ToString();

            public override IFeatureCollection Features { get; } = new FeatureCollection();

            public override IDictionary<object, object> Items { get; set; } = new ConnectionItems();
            public override IDuplexPipe Transport { get; set; }

            public override EndPoint LocalEndPoint { get; set; }

            public override EndPoint RemoteEndPoint { get; set; }

            public PipeReader Input { get; set; }

            public PipeWriter Output { get; set; }

            public override CancellationToken ConnectionClosed { get; set; }

            public HttpResponseMessage HttpResponseMessage { get; set; }

            public override void Abort(ConnectionAbortedException abortReason)
            {
                HttpResponseMessage.Dispose();

                _executionTcs.TrySetCanceled();

                Input.CancelPendingRead();
                Output.CancelPendingFlush();
            }

            public override ValueTask DisposeAsync()
            {
                HttpResponseMessage.Dispose();

                _executionTcs.TrySetResult(null);
                return base.DisposeAsync();
            }
        }

        private class HttpClientConnectionContextContent : HttpContent
        {
            private readonly HttpClientConnectionContext _connectionContext;

            public HttpClientConnectionContextContent(HttpClientConnectionContext connectionContext)
            {
                _connectionContext = connectionContext;
            }

            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                _connectionContext.Output = PipeWriter.Create(stream);

                // Immediately flush request stream to send headers
                // https://github.com/dotnet/corefx/issues/39586#issuecomment-516210081
                await stream.FlushAsync().ConfigureAwait(false);

                await _connectionContext.ExecutionTask.ConfigureAwait(false);
            }

            protected override bool TryComputeLength(out long length)
            {
                length = -1;
                return false;
            }
        }
    }
}
