using Bedrock.Framework.Protocols;
using BenchmarkDotNet.Attributes;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace Bedrock.Framework.Benchmarks
{
    /// <summary>
    /// Benchmarks for <see cref="ProtocolReader"/>.
    /// </summary>
    public class ProtocolReaderBenchmarks
    {
        private Pipe dataPipe;
        private PipeWriter writer;
        private ProtocolReader protReader;
        private SimpleReader delimitedMessageReader;
        private byte[] data;
        private int halfDataSize;

        [Params(10, 100, 1000)]
        public int MessageSize { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            // Establish as much that can be pre-calculated as possible.
            dataPipe = new Pipe();
            writer = dataPipe.Writer;
            delimitedMessageReader = new SimpleReader(MessageSize);
            data = new byte[MessageSize];
            data.AsSpan().Fill(1);
            halfDataSize = MessageSize / 2;

            protReader = new ProtocolReader(dataPipe.Reader);
        }

        /// <summary>
        /// Baseline writing the data to the pipe with out Bedrock; used to compare the cost of adding the protocol reader.
        /// </summary>
        [Benchmark(Baseline = true)]
        public async ValueTask PipeOnly()
        {
            writer.Write(data.AsSpan());
            await writer.FlushAsync();

            var result = await dataPipe.Reader.ReadAsync();

            dataPipe.Reader.AdvanceTo(result.Buffer.End);
        }

        /// <summary>
        /// Benchmark reading a stream where the entire message is always available.
        /// </summary>
        [Benchmark]
        public async ValueTask ReadProtocolWithWholeMessageAvailable()
        {
            writer.Write(data.AsSpan());
            await writer.FlushAsync();

            await protReader.ReadAsync(delimitedMessageReader);

            protReader.Advance();
        }

        /// <summary>
        /// Benchmark reading a stream where the entire message is never available.
        /// </summary>
        [Benchmark]
        public async ValueTask ReadProtocolWithPartialMessageAvailable()
        {
            writer.Write(data.AsSpan(0, halfDataSize));
            await writer.FlushAsync();

            var readTask = protReader.ReadAsync(delimitedMessageReader);

            writer.Write(data.AsSpan(halfDataSize));
            await writer.FlushAsync();

            await readTask;

            protReader.Advance();
        }

        class SimpleReader : IMessageReader<int>
        {
            private readonly int messageSize;
            

            public SimpleReader(int messageSize)
            {
                this.messageSize = messageSize;
            }

            public bool TryParseMessage(in ReadOnlySequence<byte> input, ref SequencePosition consumed, ref SequencePosition examined, out int message)
            {
                // message isn't available/complete.
                if(input.Length < messageSize)
                {
                    message = 0;
                    return false;
                }

                // Move along in the set.
                consumed = input.GetPosition(messageSize);
                examined = consumed;

                message = messageSize;

                return true;
            }
        }
    }
}
