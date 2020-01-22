using System.Buffers;
using System.Net.Http;
using System.Text;
using Bedrock.Framework.Protocols;
using Xunit;

namespace Bedrock.Framework.Tests
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
            var expected = "Host: www.host.com:8080";
            Assert.Contains(expected, actual);
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
            var expected = "Host: www.another-host.com";
            Assert.Contains(expected, actual);
        }
    }
}
