using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework.Protocols
{
    public class HttpServerProtocol
    {
        private readonly ConnectionContext _connection;
        private readonly ProtocolReader _reader;

        private readonly Http1ResponseMessageWriter _writer = new Http1ResponseMessageWriter();

        public HttpServerProtocol(ConnectionContext connection)
        {
            _connection = connection;
            _reader = connection.CreateReader();
        }

        public async ValueTask<HttpRequestMessage> ReadRequestAsync()
        {
            var content = new HttpBodyContent();
            var headerReader = new Http1RequestMessageReader(content);

            var result = await _reader.ReadAsync(headerReader).ConfigureAwait(false);

            if (result.IsCompleted)
            {
                throw new ConnectionAbortedException();
            }

            var request = result.Message;

            // TODO: Handle upgrade
            if (content.Headers.ContentLength != null)
            {
                content.SetStream(new HttpBodyStream(_reader, new ContentLengthHttpBodyReader(request.Content.Headers.ContentLength.Value)));
            }
            else if (request.Headers.TransferEncodingChunked.HasValue)
            {
                content.SetStream(new HttpBodyStream(_reader, new ChunkedHttpBodyReader()));
            }
            else
            {
                content.SetStream(new HttpBodyStream(_reader, new ContentLengthHttpBodyReader(0)));
            }

            _reader.Advance();

            return request;
        }

        public async ValueTask WriteResponseAsync(HttpResponseMessage responseMessage)
        {
            _writer.WriteMessage(responseMessage, _connection.Transport.Output);

            if (responseMessage.Content != null)
            {
                await responseMessage.Content.CopyToAsync(_connection.Transport.Output.AsStream()).ConfigureAwait(false);
            }
        }
    }
}

