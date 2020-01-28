using BenchmarkDotNet.Attributes;
using System;
using System.Buffers;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Benchmarks
{
    public class PayloadWriterBigEndianBenchmarks
    {
        [Params(100)]
        public int NumIterations { get; set; }

        private byte[] dataBaselineByte;
        private byte[] dataByte;
        private byte[] dataByteAndSize;
        private byte[] dataShort;
        private byte[] dataInt;
        private byte[] dataLong;

        [GlobalSetup]
        public void Setup()
        {
            dataBaselineByte = new byte[NumIterations * sizeof(byte)];
            dataByte = new byte[NumIterations * sizeof(byte)];
            dataByteAndSize = new byte[(NumIterations * sizeof(byte)) + sizeof(int)];
            dataShort = new byte[NumIterations * sizeof(short)];
            dataInt = new byte[NumIterations * sizeof(int)];
            dataLong = new byte[NumIterations * sizeof(long)];
        }

        [Benchmark(Baseline = true)]
        public void WriteByteToArray()
        {
            var pw = new PayloadWriter(shouldWriteBigEndian: true);

            var dataBaselineByteScratch = new byte[NumIterations * sizeof(byte)];
            var span = new Span<byte>(dataBaselineByteScratch);
            for (byte i = 0; i < NumIterations; i++)
            {
                span[i] = i;
            }

            if (pw.TryWritePayload(out var payload))
            {

            }

            span.TryCopyTo(dataBaselineByte);
        }

        [Benchmark]
        public void PayloadWriterWritesByte()
        {
            var pw = new PayloadWriter(shouldWriteBigEndian: true);

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
        public void PayloadWriterWritesByteAndSizeCalculation()
        {
            var pw = new PayloadWriter(shouldWriteBigEndian: true)
                .StartCalculatingSize(nameof(PayloadWriterWritesByteAndSizeCalculation));

            for (byte i = 0; i < NumIterations; i++)
            {
                pw.Write(i);
            }

            pw.EndSizeCalculation(nameof(PayloadWriterWritesByteAndSizeCalculation));

            if (pw.TryWritePayload(out var payload))
            {
            }

            payload.CopyTo(dataByteAndSize);
        }

        [Benchmark]
        public void PayloadWriterWritesShort()
        {
            var pw = new PayloadWriter(shouldWriteBigEndian: true);

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
            var pw = new PayloadWriter(shouldWriteBigEndian: true);

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
            var pw = new PayloadWriter(shouldWriteBigEndian: true);

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
