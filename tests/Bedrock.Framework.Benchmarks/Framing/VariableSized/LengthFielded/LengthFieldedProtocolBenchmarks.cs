using Bedrock.Framework.Experimental.Protocols.Framing.VariableSized.LengthFielded;
using Bedrock.Framework.Protocols;
using BenchmarkDotNet.Attributes;
using ServerApplication.Framing.VariableSized.LengthFielded;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace Bedrock.Framework.Benchmarks.Framing.VariableSized.LengthFielded
{
    /// <summary>
    /// Benchmarks for <see cref="LengthFieldedProtocol"/>.
    /// </summary>
    [MemoryDiagnoser]
    public class LengthFieldedProtocolBenchmarks
    {
        private readonly Pipe _dataPipe;
        private readonly PipeWriter _writer;
        private readonly HeaderFactory _headerFactory;
        private readonly int _customHeaderData;
        private readonly Header _header;
        private readonly ProtocolReader _protocolReader;
        private readonly LengthFieldedProtocol _lengthFieldedProtocol;
        private readonly byte[] _data;
        private readonly int _halfDataSize;

        [Params(10, 100, 1000)]
        public int MessageSize { get; set; }

        public LengthFieldedProtocolBenchmarks()
        {
            _dataPipe = new Pipe();
            _writer = _dataPipe.Writer;
            _data = new byte[MessageSize];
            _data.AsSpan().Fill(1);
            _halfDataSize = MessageSize / 2;

            _headerFactory = new HeaderFactory();
            _customHeaderData = 0;
            _header = _headerFactory.CreateHeader(_data.Length, _customHeaderData);
            _lengthFieldedProtocol = new LengthFieldedProtocol(Helper.HeaderLength, (headerSequence) => _headerFactory.CreateHeader(headerSequence));
            _protocolReader = new ProtocolReader(_dataPipe.Reader);
        }

        /// <summary>
        /// Baseline writing the data to the pipe with out Bedrock; used to compare the cost of adding the Lenght Fielded Protocol.
        /// </summary>
        [Benchmark(Baseline = true)]
        public async ValueTask PipeOnly()
        {
            _writer.Write(_header.AsSpan());
            _writer.Write(_data.AsSpan());
            await _writer.FlushAsync();

            var result = await _dataPipe.Reader.ReadAsync();

            _dataPipe.Reader.AdvanceTo(result.Buffer.End);
        }


        /// <summary>
        /// Benchmark reading a stream where the entire message is always available.
        /// </summary>
        [Benchmark]
        public async ValueTask ReadProtocolWithWholeMessageAvailable()
        {
            _writer.Write(_header.AsSpan());
            _writer.Write(_data.AsSpan());
            await _writer.FlushAsync();

            await _protocolReader.ReadAsync(_lengthFieldedProtocol);

            _protocolReader.Advance();
        }

        /// <summary>
        /// Benchmark reading a stream where the entire message is never available.
        /// </summary>
        [Benchmark]
        public async ValueTask ReadProtocolWithPartialMessageAvailable()
        {
            _writer.Write(_header.AsSpan());
            _writer.Write(_data.AsSpan(0, _halfDataSize));
            await _writer.FlushAsync();

            var readTask = _protocolReader.ReadAsync(_lengthFieldedProtocol);

            _writer.Write(_data.AsSpan(_halfDataSize));
            await _writer.FlushAsync();

            await readTask;

            _protocolReader.Advance();
        }
    }
}
