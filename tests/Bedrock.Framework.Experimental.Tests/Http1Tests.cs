using System.Buffers;
using System.Net.Http;
using System.Text;
using Bedrock.Framework.Protocols;
using Xunit;

namespace Bedrock.Framework.Experimental.Tests
{
    public class Http1Tests
    {
        [Fact]
        public void WriteHttp1MessageWithNoHost()
        {
            // arrange
            var messageWriter = new Http1RequestMessageWriter("www.host.com", 8080);

            var request = new HttpRequestMessage(HttpMethod.Get, "/api");
            var output = new ArrayBufferWriter<byte>();

            // act
            messageWriter.WriteMessage(request, output);
            var actual = Encoding.ASCII.GetString(output.WrittenSpan);

            // assert
            var expected = "GET /api HTTP/1.1\r\nHost: www.host.com:8080\r\n\r\n";
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void WriteHttp1MessageWithHost()
        {
            // arrange
            var messageWriter = new Http1RequestMessageWriter("www.host.com", 80);

            var request = new HttpRequestMessage(HttpMethod.Post, "/api");
            request.Headers.Host = "www.another-host.com";
            var output = new ArrayBufferWriter<byte>();

            // act
            messageWriter.WriteMessage(request, output);
            var actual = Encoding.ASCII.GetString(output.WrittenSpan);

            // assert
            var expected = "POST /api HTTP/1.1\r\nHost: www.another-host.com\r\n\r\n";
            Assert.Equal(expected, actual);
        }
    }
}
