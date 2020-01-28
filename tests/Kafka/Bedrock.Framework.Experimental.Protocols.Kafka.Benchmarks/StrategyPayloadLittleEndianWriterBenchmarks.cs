using BenchmarkDotNet.Attributes;
using System;
using System.Buffers;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Benchmarks
{
    public class StrategyPayloadWriterLittleEndianBenchmarks
    {
        [Params(100)]
        public int NumIterations { get; set; }

        private byte[] dataInt;
        private byte[] dataLong;
        private byte[] dataShort;
        private byte[] dataByte;

        [GlobalSetup]
        public void Setup()
        {
            dataInt = new byte[NumIterations * sizeof(int)];
            dataLong = new byte[NumIterations * sizeof(long)];
            dataShort = new byte[NumIterations * sizeof(short)];
            dataByte = new byte[NumIterations * sizeof(byte)];
        }

        [Benchmark(Baseline = true)]
        public void StrategyPayloadWriterWritesByte()
        {
            var pw = new StrategyPayloadWriter(shouldWriteBigEndian: false);

            for (byte i = 0; i < NumIterations; i++)
            {
                pw.Write(i);
            }

            if (pw.TryWritePayload(out var payload))
            {
            }

            payload.CopyTo(dataByte);
        }

        [Benchmark]
        public void StrategyPayloadWriterWritesShort()
        {
            var pw = new StrategyPayloadWriter(shouldWriteBigEndian: false);

            for (short i = 0; i < NumIterations; i++)
            {
                pw.Write(i);
            }

            if (pw.TryWritePayload(out var payload))
            {
            }

            payload.CopyTo(dataShort);
        }

        [Benchmark]
        public void StrategyPayloadWriterWritesInt()
        {
            var pw = new StrategyPayloadWriter(shouldWriteBigEndian: false);

            for (int i = 0; i < NumIterations; i++)
            {
                pw.Write(i);
            }

            if (pw.TryWritePayload(out var payload))
            {
            }

            payload.CopyTo(dataInt);
        }

        [Benchmark]
        public void StrategyPayloadWriterWritesLong()
        {
            var pw = new StrategyPayloadWriter(shouldWriteBigEndian: false);

            for (long i = 0; i < NumIterations; i++)
            {
                pw.Write(i);
            }

            if (pw.TryWritePayload(out var payload))
            {
            }

            payload.CopyTo(dataLong);
        }
    }
}
