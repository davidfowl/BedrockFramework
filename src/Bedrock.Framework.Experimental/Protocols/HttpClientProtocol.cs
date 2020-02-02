using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework.Protocols
{
    public class HttpClientProtocol
    {
        private readonly ConnectionContext _connection;
        private readonly ProtocolReader _reader;
        private readonly Http1RequestMessageWriter _messageWriter;

        public HttpClientProtocol(ConnectionContext connection)
        {
            _connection = connection;
            _reader = connection.CreateReader();

            (string host, int port) = connection.RemoteEndPoint switch
            {
                UriEndPoint uriEndPoint => (uriEndPoint.Uri.Host, uriEndPoint.Uri.Port),
                IPEndPoint ip => (ip.Address.ToString(), ip.Port),
                NamedPipeEndPoint np => (np.PipeName, 80),
                _ => throw new NotSupportedException($"{connection.RemoteEndPoint} not supported")
            };
            _messageWriter = new Http1RequestMessageWriter(host, port);
        }

        public async ValueTask<HttpResponseMessage> SendAsync(HttpRequestMessage requestMessage, HttpCompletionOption completionOption = HttpCompletionOption.ResponseHeadersRead, System.Threading.CancellationToken cancellationToken = default)
        {
            // Write request message headers
            _messageWriter.WriteMessage(requestMessage, _connection.Transport.Output);

            // Write the body directly
            if (requestMessage.Content != null)
            {
                await requestMessage.Content.CopyToAsync(_connection.Transport.Output.AsStream()).ConfigureAwait(false);
            }

            await _connection.Transport.Output.FlushAsync(cancellationToken).ConfigureAwait(false);

            var content = new HttpBodyContent();
            var headerReader = new Http1ResponseMessageReader(content);

            var result = await _reader.ReadAsync(headerReader, cancellationToken).ConfigureAwait(false);

            if (result.IsCompleted)
            {
                throw new ConnectionAbortedException();
            }

            var response = result.Message;

            // TODO: Handle upgrade
            if (content.Headers.ContentLength != null)
            {
                content.SetStream(new HttpBodyStream(_reader, new ContentLengthHttpBodyReader(response.Content.Headers.ContentLength.Value)));
            }
            else if (response.Headers.TransferEncodingChunked.HasValue)
            {
                content.SetStream(new HttpBodyStream(_reader, new ChunkedHttpBodyReader()));
            }
            else
            {
                content.SetStream(new HttpBodyStream(_reader, new ContentLengthHttpBodyReader(0)));
            }

            _reader.Advance();

            return response;
        }
    }
}
