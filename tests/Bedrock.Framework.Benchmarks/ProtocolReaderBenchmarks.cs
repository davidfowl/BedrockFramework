using Bedrock.Framework.Protocols;
using BenchmarkDotNet.Attributes;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace Bedrock.Framework.Benchmarks
{
    public class ProtocolReaderBenchmarks
    {
        private Pipe dataPipe;
        private SimpleDelimitedReader delimitedMessageReader;
        
        [Params(1, 10, 100, 1000, 10000)]
        public int ChunkCount { get; set; }

        [Params(10)]
        public int ChunkSize { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            dataPipe = new Pipe();

            delimitedMessageReader = new SimpleDelimitedReader(ChunkSize);
        }

        class SimpleDelimitedReader : IMessageReader<int>
        {
            private readonly int chunkSize;
            

            public SimpleDelimitedReader(int chunkSize)
            {
                this.chunkSize = chunkSize;
            }

            public bool TryParseMessage(in ReadOnlySequence<byte> input, ref SequencePosition consumed, ref SequencePosition examined, out int message)
            {
                if(input.IsEmpty)
                {
                    message = 0;
                    return false;
                }

                // Move along in the set.
                consumed = input.GetPosition(chunkSize);
                examined = consumed;

                message = chunkSize;

                return true;
            }
        }

        /// <summary>
        /// Benchmark reading a stream consisting of chunks of pre-determined lengths of data.
        /// </summary>
        [Benchmark]
        public async ValueTask ReadChunkedProtocol()
        {
            var writeMemory = dataPipe.Writer.GetMemory(ChunkSize * ChunkCount);
            // Tell the pipe we've put all the required data in memory (we haven't).
            dataPipe.Writer.Advance(ChunkSize * ChunkCount);
            dataPipe.Writer.Complete();
            
            var reader = new ProtocolReader(dataPipe.Reader);
#if DEBUG
            int chunkCounts = 0;
#endif
            // Read the chunks.
            while(!(await reader.ReadAsync(delimitedMessageReader)).IsCompleted)
            {
#if DEBUG
                chunkCounts++;
#endif
                reader.Advance();
            }

#if DEBUG
            if(chunkCounts != ChunkCount)
            {
                throw new ApplicationException("Didn't read all the chunks");
            }
#endif
            dataPipe.Reader.Complete();

            // Reset the pipe back to 0.
            dataPipe.Reset();
        }
    }
}
