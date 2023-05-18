using BenchmarkDotNet.Attributes;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading.Tasks;
using System.Threading;
using Bedrock.Framework.Protocols;

namespace Bedrock.Framework.Benchmarks
{
    /// <summary>
    /// Benchmarks for <see cref="ProtocolWriter"/>.
    /// </summary>
    public class ProtocolWriterBenchmarks
    {
        private SemaphoreSlim singleWriter;
        private SimpleWriter simpleWriter;
        private ProtocolWriter protocol;
        private PipeWriter writer;
        private byte[] data;

        [Params(10, 100, 1000, 10000)]
        public int MessageSize { get; set; }

        [IterationSetup]
        public void Setup()
        {
            // Establish as much that can be pre-calculated as possible.
            var dataPipe = new Pipe(new PipeOptions(useSynchronizationContext: false));
            writer = dataPipe.Writer;
            simpleWriter = new SimpleWriter();
            singleWriter = new SemaphoreSlim(1, 1);

            data = new byte[MessageSize];
            data.AsSpan().Fill(1);

            protocol = new ProtocolWriter(dataPipe.Writer, singleWriter);
        }

        /// <summary>
        /// Baseline writing the data to the pipe with out Bedrock; used to compare the cost of adding the protocol writer.
        /// </summary>
        [Benchmark(Baseline = true)]
        public async ValueTask PipeOnly()
        {
            writer.Write(data.AsSpan());
            await writer.FlushAsync();
        }

        /// <summary>
        /// Benchmark writing a message where the writer is always directly available.
        /// </summary>
        [Benchmark]
        public async ValueTask WriteMessageWithWriterAvailable()
        {
            await protocol.WriteAsync(simpleWriter, data);
        }

        /// <summary>
        /// Benchmark writing a message where the writer is never available.
        /// </summary>
        [Benchmark]
        public async ValueTask WriteMessageWithWriterUnavailable()
        {
            singleWriter.Wait();
            var writeAsync = protocol.WriteAsync(simpleWriter, data);
            singleWriter.Release();

            await writeAsync;
        }

        class SimpleWriter : IMessageWriter<byte[]>
        {
            public void WriteMessage(byte[] message, IBufferWriter<byte> output)
                => output.Write(message.AsSpan());
        }
    }
}
