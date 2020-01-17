using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework.Protocols
{
    public class HttpClientProtocol
    {
        private readonly ConnectionContext _connection;
        private readonly ProtocolReader _reader;
        private readonly Http1RequestMessageWriter _messageWriter = new Http1RequestMessageWriter();

        public HttpClientProtocol(ConnectionContext connection)
        {
            _connection = connection;
            _reader = connection.CreateReader();
        }

        public async ValueTask<HttpResponseMessage> SendAsync(HttpRequestMessage requestMessage, HttpCompletionOption completionOption = HttpCompletionOption.ResponseHeadersRead)
        {
            // Write request message headers
            _messageWriter.WriteMessage(ref requestMessage, _connection.Transport.Output);

            // Write the body directly
            if (requestMessage.Content != null)
            {
                await requestMessage.Content.CopyToAsync(_connection.Transport.Output.AsStream()).ConfigureAwait(false);
            }

            await _connection.Transport.Output.FlushAsync().ConfigureAwait(false);

            var content = new HttpBodyContent();
            var headerReader = new Http1ResponseMessageReader(content);

            var result = await _reader.ReadAsync(headerReader).ConfigureAwait(false);

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
