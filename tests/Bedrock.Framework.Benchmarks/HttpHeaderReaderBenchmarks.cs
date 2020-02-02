using Bedrock.Framework.Protocols.Http.Http1;
using BenchmarkDotNet.Attributes;
using System;
using System.Buffers;
using System.Text;

namespace Bedrock.Framework.Benchmarks
{
    public class HttpHeaderReaderBenchmarks
    {
        [Params(10, 1000)]
        public int HeaderValueLength { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            var rand = new Random(42);
            var stringbuilder = new StringBuilder(HeaderValueLength);
            for(var i = 0; i < HeaderValueLength; i++)
            {
                var offset = rand.Next() % 26;
                stringbuilder.Append((char)('a' + offset));
            }
            bytes = new ReadOnlySequence<byte>(Encoding.ASCII.GetBytes($"Header-Name:{stringbuilder}\r\n"));
        }

        private ReadOnlySequence<byte> bytes;
        private Http1HeaderReader reader = new Http1HeaderReader();

        [Benchmark]
        public bool ReadHeader()
        {
            var consumed = default(SequencePosition);
            var examined = default(SequencePosition);
            return reader.TryParseMessage(in bytes, ref consumed, ref examined, out var result);
        }
    }
}
