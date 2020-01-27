using BenchmarkDotNet.Running;
using System;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            new BenchmarkSwitcher(AllBenchmarks)
                .Run(args, new BenchmarkConfig());
        }

        private static readonly Type[] AllBenchmarks = new[]
        {
            typeof(PayloadWriterBigEndianBenchmarks),
            typeof(PayloadWriterLittleEndianBenchmarks),
            typeof(StrategyPayloadWriterBigEndianBenchmarks),
            typeof(StrategyPayloadWriterLittleEndianBenchmarks),
        };
    }
}
