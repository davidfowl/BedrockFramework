using BenchmarkDotNet.Running;
using System;

namespace Bedrock.Framework.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            new BenchmarkSwitcher(AllBenchmarks).Run(args, new BenchmarkConfig());
        }

        private static readonly Type[] AllBenchmarks = new[]
        {
            typeof(ProtocolReaderBenchmarks),
            typeof(MessagePipeReaderBenchmarks),
            typeof(WebSocketProtocolBenchmarks)
            typeof(HttpHeaderReaderBenchmarks)
        };
    }
}
