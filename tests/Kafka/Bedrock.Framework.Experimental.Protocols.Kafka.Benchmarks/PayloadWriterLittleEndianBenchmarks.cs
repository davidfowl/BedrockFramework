using BenchmarkDotNet.Attributes;
using System;
using System.Buffers;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Benchmarks
{
    public class PayloadWriterLittleEndianBenchmarks
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
        public void PayloadWriterWritesByte()
        {
            var pw = new PayloadWriter(isBigEndian: false);

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
        public void PayloadWriterWritesShort()
        {
            var pw = new PayloadWriter(isBigEndian: false);

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
        public void PayloadWriterWritesInt()
        {
            var pw = new PayloadWriter(isBigEndian: false);

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
        public void PayloadWriterWritesLong()
        {
            var pw = new PayloadWriter(isBigEndian: false);

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
