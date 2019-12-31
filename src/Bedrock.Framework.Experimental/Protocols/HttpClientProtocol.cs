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

        private HttpClientProtocol(ConnectionContext connection)
        {
            _connection = connection;
            _reader = _connection.CreateReader();
        }

        public async ValueTask<HttpResponseMessage> SendAsync(HttpRequestMessage requestMessage, HttpCompletionOption completionOption = HttpCompletionOption.ResponseHeadersRead)
        {
            // Write request message headers
            _messageWriter.WriteMessage(requestMessage, _connection.Transport.Output);

            // Write the body directly
            if (requestMessage.Content != null)
            {
                await requestMessage.Content.CopyToAsync(_connection.Transport.Output.AsStream()).ConfigureAwait(false);
            }

            await _connection.Transport.Output.FlushAsync().ConfigureAwait(false);

            var headerReader = new Http1ResponseMessageReader();
            var content = new HttpProtocolContent();
            headerReader.SetResponse(new HttpResponseMessage() { Content = content });

            var result = await _reader.ReadAsync(headerReader).ConfigureAwait(false);

            if (result.IsCompleted)
            {
                throw new ConnectionAbortedException();
            }

            var response = result.Message;

            if (response.Content.Headers.ContentLength != null)
            {
                content.SetStream(new HttpBodyStream(_reader, new ContentLengthHttpBodyReader(response.Content.Headers.ContentLength.Value)));
            }
            else // TODO: Handle upgrade
            {
                content.SetStream(new HttpBodyStream(_reader, new ChunkedHttpBodyReader()));
            }

            _reader.Advance();

            return response;
        }

        public static HttpClientProtocol CreateFromConnection(ConnectionContext connection)
        {
            return new HttpClientProtocol(connection);
        }
    }
}
