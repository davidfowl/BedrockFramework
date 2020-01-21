using Bedrock.Framework.Protocols;
using BenchmarkDotNet.Attributes;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace Bedrock.Framework.Benchmarks
{
    public class MessagePipeReaderBenchmarks
    {
        [Params(4096)]
        public int MessageSize { get; set; }

        [Params(100)]
        public int NumIterations { get; set; }

        private Pipe pipe;
        private TestProtocol testProtocol;
        private byte[] data;

        [GlobalSetup]
        public void Setup()
        {
            pipe = new Pipe();
            testProtocol = new TestProtocol();
            data = new byte[MessageSize];
            data.AsSpan().Fill(1);
        }

        /// <summary>
        /// Baseline writing and reading from the pipe without using the MessagePipeReader.
        /// Used to compare the costs of adding the MessagePipeReader.
        /// </summary>
        [Benchmark(Baseline = true)]
        public async ValueTask PipeReaderConsumeAllEachTime()
        {
            var reader = pipe.Reader;
            var writer = pipe.Writer;
            for (var i = 0; i < NumIterations; i++)
            {
                testProtocol.WriteMessage(data, writer);
                await writer.FlushAsync().ConfigureAwait(false);
                var result = await reader.ReadAsync();
                reader.AdvanceTo(result.Buffer.End);
            }
        }

        [Benchmark]
        public async ValueTask MessagePipeReaderConsumeAllEachTime()
        {
            var reader = new MessagePipeReader(pipe.Reader, testProtocol);
            var writer = pipe.Writer;
            for (var i = 0; i < NumIterations; i++)
            {
                testProtocol.WriteMessage(data, writer);
                await writer.FlushAsync().ConfigureAwait(false);
                var result = await reader.ReadAsync();
                reader.AdvanceTo(result.Buffer.End);
            }
            reader.Complete();
        }

        [Benchmark]
        public async ValueTask MessagePipeReaderConsumeNoneEachTime()
        {
            var reader = new MessagePipeReader(pipe.Reader, testProtocol);
            var writer = pipe.Writer;
            for (var i = 0; i < NumIterations; i++)
            {
                testProtocol.WriteMessage(data, writer);
                await writer.FlushAsync().ConfigureAwait(false);
                var result = await reader.ReadAsync();
                reader.AdvanceTo(result.Buffer.Start);
            }
            reader.Complete();
        }

        private class TestProtocol : IMessageReader<ReadOnlySequence<byte>>, IMessageWriter<byte[]>
        {
            public bool TryParseMessage(in ReadOnlySequence<byte> input, ref SequencePosition consumed, ref SequencePosition examined, out ReadOnlySequence<byte> message)
            {
                var reader = new SequenceReader<byte>(input);

                if (!reader.TryReadLittleEndian(out int length) || reader.Remaining < length)
                {
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
