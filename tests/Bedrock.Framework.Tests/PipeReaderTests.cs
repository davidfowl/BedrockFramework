using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bedrock.Framework.Protocols;
using Microsoft.AspNetCore.Connections;
using Xunit;

namespace Bedrock.Framework.Tests
{
    public class PipeReaderTests
    {
        [Fact]
        public async Task ReadMessagesAsynchronouslyWorks()
        {
            var options = new PipeOptions(useSynchronizationContext: false, readerScheduler: PipeScheduler.Inline, writerScheduler: PipeScheduler.Inline);
            var pair = DuplexPipe.CreateConnectionPair(options, options);
            await using var connection = new DefaultConnectionContext(Guid.NewGuid().ToString(), pair.Transport, pair.Application);
            var data = Encoding.UTF8.GetBytes("Hello World");
            var protocol = new TestProtocol();

            async Task WritingTask()
            {
                var writer = connection.Application.Output;

                for (int i = 0; i < 3; i++)
                {
                    protocol.WriteMessage(data, writer);
                    await writer.FlushAsync().ConfigureAwait(false);
                }

                await writer.CompleteAsync().ConfigureAwait(false);
            }

            async Task ReadingTask()
            {
                var reader = connection.CreatePipeReader(protocol);

                while (true)
                {
                    var result = await reader.ReadAsync().ConfigureAwait(false);
                    var buffer = result.Buffer;

                    if (buffer.Length < 3 * data.Length)
                    {
                        reader.AdvanceTo(buffer.Start, buffer.End);
                        continue;
                    }

                    Assert.Equal(Enumerable.Repeat(data, 3).SelectMany(a => a).ToArray(), buffer.ToArray());

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }

                await reader.CompleteAsync();
            }

            var readingTask = ReadingTask();
            var writingTask = WritingTask();

            await writingTask;
            await readingTask;
        }

        private class TestProtocol : IMessageReader<ReadOnlySequence<byte>>, IMessageWriter<byte[]>
        {
            public bool TryParseMessage(in ReadOnlySequence<byte> input, out SequencePosition consumed, out SequencePosition examined, out ReadOnlySequence<byte> message)
            {
                var reader = new SequenceReader<byte>(input);

                if (reader.TryReadLittleEndian(out int length) && reader.Remaining < length)
                {
                    consumed = input.Start;
                    examined = input.End;
                    message = default;
                    return false;
                }

                message = input.Slice(reader.Position, length);
                consumed = message.End;
                examined = message.End;
                return true;
            }

            public void WriteMessage(byte[] message, IBufferWriter<byte> output)
            {
                var span = output.GetSpan(4);
                BinaryPrimitives.WriteInt32LittleEndian(span, message.Length);
                output.Advance(4);
                output.Write(message);
            }
        }
    }
}
