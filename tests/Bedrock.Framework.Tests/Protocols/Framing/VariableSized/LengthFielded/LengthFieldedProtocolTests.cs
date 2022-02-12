using System;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Bedrock.Framework.Experimental.Protocols.Framing.VariableSized.LengthFielded;
using ServerApplication.Framing.VariableSized.LengthFielded;
using Bedrock.Framework.Protocols;
using System.IO;
using System.IO.Pipelines;

namespace Bedrock.Framework.Tests.Protocols.Framing.VariableSized.LengthFielded
{
    public class LengthFieldedProtocolTests
    {
        private static ProtocolReader CreateReader(LengthFieldedProtocol protocol, out Func<Frame, Task> writeFunc)
        {
            var stream = new MemoryStream();
            var writer = PipeWriter.Create(stream);
            var reader = new ProtocolReader(PipeReader.Create(stream));
            
            long written = 0;
            writeFunc = async frame =>
            {
                var position = stream.Position;
                stream.Position = written;
                protocol.WriteMessage(frame, writer);
                await writer.FlushAsync().ConfigureAwait(false);
                written = stream.Position;
                stream.Position = position;
            };
            return reader;
        }

        [Fact]
        public async Task SingleMessageWorks()
        {
            // Arrange
            var headerFactory = new HeaderFactory();
            var protocol = new LengthFieldedProtocol(Helper.HeaderLength, (headerSequence) => headerFactory.CreateHeader(headerSequence));
            var reader = CreateReader(protocol, out var writeFunc);

            string payload = "This is a test payload.";
            int customHeaderData = 123;
            var payloadAsArray = Encoding.ASCII.GetBytes(payload);
            var header = headerFactory.CreateHeader(payloadAsArray.Length, customHeaderData);
            var frame = new Frame(header, payloadAsArray);

            // Act
            await writeFunc(frame);
            var readResult = await reader.ReadAsync(protocol);
            var result = readResult.Message;
            reader.Advance();

            // Assert
            Assert.Equal(header, result.Header);
#if NETCOREAPP3_1
            Assert.Equal(payload, Encoding.ASCII.GetString(result.Payload.ToArray()));
#elif NET6_0_OR_GREATER
            Assert.Equal(payload, Encoding.ASCII.GetString(result.Payload));
#endif
        }

        [Fact]
        public async Task MultipleMessagesWorks()
        {
            // Arrange
            var headerFactory = new HeaderFactory();
            var protocol = new LengthFieldedProtocol(Helper.HeaderLength, (headerSequence) => headerFactory.CreateHeader(headerSequence));
            var reader = CreateReader(protocol, out var writeFunc);

            string payload = "This is a test payload.";
            int customHeaderData = 123;
            var payloadAsArray = Encoding.ASCII.GetBytes(payload);
            var header = headerFactory.CreateHeader(payloadAsArray.Length, customHeaderData);
            var frame = new Frame(header, payloadAsArray);

            // Act
            for (var i = 0; i < 5; i++)
            {
                await writeFunc(frame);
            }

            // Assert
            for (var i = 0; i < 5; i++)
            {
                var readResult = await reader.ReadAsync(protocol);
                var result = readResult.Message;
                reader.Advance();

                Assert.Equal(header, result.Header);
#if NETCOREAPP3_1
                Assert.Equal(payload, Encoding.ASCII.GetString(result.Payload.ToArray()));
#elif NET6_0_OR_GREATER
                Assert.Equal(payload, Encoding.ASCII.GetString(result.Payload));
#endif
            }
        }

    }
}
