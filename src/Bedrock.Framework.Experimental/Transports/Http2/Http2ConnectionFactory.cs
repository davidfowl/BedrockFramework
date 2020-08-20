using Microsoft.AspNetCore.Connections;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Connections;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework
{
    public class Http2ConnectionFactory : ConnectionFactory
    {
        private readonly HttpClient _client = new HttpClient();

        public override async ValueTask<Connection> ConnectAsync(EndPoint endPoint, IConnectionProperties options = null, CancellationToken cancellationToken = default)
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
            var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            connection.HttpResponseMessage = response;
            var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            connection.Input = PipeReader.Create(responseStream);

            return connection;
        }

        private class HttpClientConnectionContext : Connection, IDuplexPipe, IConnectionProperties
        {
            private readonly TaskCompletionSource<object> _executionTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            public HttpClientConnectionContext()
            {
                Transport = this;
            }

            public Task ExecutionTask => _executionTcs.Task;

            public IDuplexPipe Transport { get; set; }

            public PipeReader Input { get; set; }

            public PipeWriter Output { get; set; }

            public HttpResponseMessage HttpResponseMessage { get; set; }

            public override IConnectionProperties ConnectionProperties => this;

            public override EndPoint LocalEndPoint { get; }

            public override EndPoint RemoteEndPoint { get; }

            public bool TryGet(Type propertyKey, [NotNullWhen(true)] out object property)
            {
                property = null;
                return false;
            }

            protected override ValueTask CloseAsyncCore(ConnectionCloseMethod method, CancellationToken cancellationToken)
            {
                switch (method)
                {
                    case ConnectionCloseMethod.GracefulShutdown:
                        HttpResponseMessage.Dispose();

                        _executionTcs.TrySetResult(null);
                        break;
                    case ConnectionCloseMethod.Abort:
                    case ConnectionCloseMethod.Immediate:
                        HttpResponseMessage.Dispose();

                        _executionTcs.TrySetCanceled();

                        Input.CancelPendingRead();
                        Output.CancelPendingFlush();
                        break;
                    default:
                        break;
                }

                return default;
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