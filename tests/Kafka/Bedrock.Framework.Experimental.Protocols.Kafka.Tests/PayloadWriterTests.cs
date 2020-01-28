using System;
using System.Buffers;
using Xunit;
using Bedrock.Framework.Experimental.Protocols.Kafka;

namespace Bedrock.Framework.Experimental.Protocols.Kafka.Tests
{
    public class PayloadWriterTests
    {
        [Fact]
        public void CanWriteInt32BigEndianOnce()
        {
            int testInt = 1337;
            var pw = new PayloadWriter(shouldWriteBigEndian: true)
                .Write(testInt);

            Assert.True(pw.TryWritePayload(out var payload));
            Assert.Equal(sizeof(int), payload.Length);

            var sr = new SequenceReader<byte>(payload);
            Assert.True(sr.Length == sizeof(int));

            Assert.True(sr.TryReadBigEndian(out int value));
            Assert.Equal(testInt, value);
        }

        [Fact]
        public void CanWriteInt32BigEndianTwice()
        {
            int testInt1 = 1337;
            int testInt2 = 7331;

            var pw = new PayloadWriter(shouldWriteBigEndian: true)
                .Write(testInt1)
                .Write(testInt2);

            Assert.True(pw.TryWritePayload(out var payload));
            Assert.Equal(sizeof(int) * 2, payload.Length);

            var sr = new SequenceReader<byte>(payload);
            Assert.True(sr.Length == sizeof(int) * 2);

            Assert.True(sr.TryReadBigEndian(out int value1));
            Assert.Equal(testInt1, value1);

            Assert.True(sr.TryReadBigEndian(out int value2));
            Assert.Equal(testInt2, value2);
        }

        [Fact]
        public void CanWriteInt32LittleEndianTwice()
        {
            int testInt1 = 1337;
            int testInt2 = 7331;

            var pw = new PayloadWriter(shouldWriteBigEndian: false)
                .Write(testInt1)
                .Write(testInt2);

            Assert.True(pw.TryWritePayload(out var payload));
            Assert.Equal(sizeof(int) * 2, payload.Length);

            var sr = new SequenceReader<byte>(payload);
            Assert.True(sr.Length == sizeof(int) * 2);

            Assert.True(sr.TryReadLittleEndian(out int value1));
            Assert.Equal(testInt1, value1);

            Assert.True(sr.TryReadLittleEndian(out int value2));
            Assert.Equal(testInt2, value2);
        }

        [Fact]
        public void CanWriteInt32BigEndianAcrossChildWriter()
        {
            int testInt1 = 1337;
            int testInt2 = 7331;

            var pw1 = new PayloadWriter(shouldWriteBigEndian: true)
                .Write(testInt1);

            Assert.Equal(4, pw1.Context.BytesWritten);
            pw1.Context.CreatePayloadWriter()
                .Write(testInt2);

            Assert.Equal(8, pw1.Context.BytesWritten);

            Assert.True(pw1.TryWritePayload(out var payload));
            Assert.Equal(sizeof(int) * 2, payload.Length);

            var sr = new SequenceReader<byte>(payload);
            Assert.True(sr.Length == sizeof(int) * 2);

            Assert.True(sr.TryReadBigEndian(out int value1));
            Assert.Equal(testInt1, value1);

            Assert.True(sr.TryReadBigEndian(out int value2));
            Assert.Equal(testInt2, value2);
        }

        [Fact]
        public void CanWriteInt32LittleEndianAcrossChildWriter()
        {
            int testInt1 = 1337;
            int testInt2 = 7331;

            var pw1 = new PayloadWriter(shouldWriteBigEndian: false).Write(testInt1);
            pw1.Context.CreatePayloadWriter().Write(testInt2);

            Assert.True(pw1.TryWritePayload(out var payload));
            Assert.Equal(sizeof(int) * 2, payload.Length);

            var sr = new SequenceReader<byte>(payload);
            Assert.True(sr.Length == sizeof(int) * 2);

            Assert.True(sr.TryReadLittleEndian(out int value1));
            Assert.Equal(testInt1, value1);

            Assert.True(sr.TryReadLittleEndian(out int value2));
            Assert.Equal(testInt2, value2);
        }

        [Fact]
        public void ChildWriterCanWriteEntirePayload()
        {
            int testInt1 = 1337;
            int testInt2 = 7331;

            var pw1 = new PayloadWriter(shouldWriteBigEndian: false)
                .Write(testInt1);

            var pw2 = pw1.Context.CreatePayloadWriter()
                .Write(testInt2);

            Assert.True(pw2.TryWritePayload(out var payload));
            Assert.Equal(sizeof(int) * 2, payload.Length);

            var sr = new SequenceReader<byte>(payload);
            Assert.True(sr.Length == sizeof(int) * 2);

            Assert.True(sr.TryReadLittleEndian(out int value1));
            Assert.Equal(testInt1, value1);

            Assert.True(sr.TryReadLittleEndian(out int value2));
            Assert.Equal(testInt2, value2);
        }

        [Fact]
        public void CanWriteInt32BigEndianAcrossChildWriterToChildWriter()
        {
            int testInt1 = 1337;
            int testInt2 = 7331;
            int testInt3 = 42;

            var pw1 = new PayloadWriter(shouldWriteBigEndian: true)
                .Write(testInt1);

            var pw2 = pw1.Context.CreatePayloadWriter()
                .Write(testInt2);

            pw2.Context.CreatePayloadWriter()
                .Write(testInt3);

            Assert.True(pw1.TryWritePayload(out var payload));
            Assert.Equal(sizeof(int) * 3, payload.Length);

            var sr = new SequenceReader<byte>(payload);
            Assert.True(sr.Length == sizeof(int) * 3);

            Assert.True(sr.TryReadBigEndian(out int value1));
            Assert.Equal(testInt1, value1);

            Assert.True(sr.TryReadBigEndian(out int value2));
            Assert.Equal(testInt2, value2);

            Assert.True(sr.TryReadBigEndian(out int value3));
            Assert.Equal(testInt3, value3);
        }

        [Fact]
        public void CanWriteInt32LittleEndianAcrossChildWriterToChildWriter()
        {
            int testInt1 = 1337;
            int testInt2 = 7331;
            int testInt3 = 42;

            var pw1 = new PayloadWriter(shouldWriteBigEndian: false).Write(testInt1);
            var pw2 = pw1.Context.CreatePayloadWriter().Write(testInt2);
            pw2.Context.CreatePayloadWriter().Write(testInt3);

            Assert.True(pw1.TryWritePayload(out var payload));
            Assert.Equal(sizeof(int) * 3, payload.Length);

            var sr = new SequenceReader<byte>(payload);
            Assert.True(sr.Length == sizeof(int) * 3);

            Assert.True(sr.TryReadLittleEndian(out int value1));
            Assert.Equal(testInt1, value1);

            Assert.True(sr.TryReadLittleEndian(out int value2));
            Assert.Equal(testInt2, value2);

            Assert.True(sr.TryReadLittleEndian(out int value3));
            Assert.Equal(testInt3, value3);
        }

        [Fact]
        public void CanWriteInt16BigEndianOnce()
        {
            short testShort = 1337;
            var pw = new PayloadWriter(shouldWriteBigEndian: true)
                .Write(testShort);

            Assert.True(pw.TryWritePayload(out var payload));
            Assert.Equal(sizeof(short), payload.Length);

            var sr = new SequenceReader<byte>(payload);
            Assert.True(sr.Length == sizeof(short));

            Assert.True(sr.TryReadBigEndian(out short value));
            Assert.Equal(testShort, value);
        }

        [Fact]
        public void CanWriteInt16LittleEndianOnce()
        {
            short testShort = 1337;
            var pw = new PayloadWriter(shouldWriteBigEndian: false)
                .Write(testShort);

            Assert.True(pw.TryWritePayload(out var payload));
            Assert.Equal(sizeof(short), payload.Length);

            var sr = new SequenceReader<byte>(payload);
            Assert.True(sr.Length == sizeof(short));

            Assert.True(sr.TryReadLittleEndian(out short value));
            Assert.Equal(testShort, value);
        }

        [Fact]
        public void CanWriteInt64BigEndianOnce()
        {
            long testLong = long.MaxValue;
            var pw = new PayloadWriter(shouldWriteBigEndian: true)
                .Write(testLong);

            Assert.True(pw.TryWritePayload(out var payload));
            Assert.Equal(sizeof(long), payload.Length);

            var sr = new SequenceReader<byte>(payload);
            Assert.True(sr.Length == sizeof(long));

            Assert.True(sr.TryReadBigEndian(out long value));
            Assert.Equal(testLong, value);
        }

        [Fact]
        public void CanWriteInt64LittleEndianOnce()
        {
            long testLong = long.MaxValue;
            var pw = new PayloadWriter(shouldWriteBigEndian: false)
                .Write(testLong);

            Assert.True(pw.TryWritePayload(out var payload));
            Assert.Equal(sizeof(long), payload.Length);

            var sr = new SequenceReader<byte>(payload);
            Assert.True(sr.Length == sizeof(long));

            Assert.True(sr.TryReadLittleEndian(out long value));
            Assert.Equal(testLong, value);
        }

        [Fact]
        public void CanCalculateSize()
        {
            long testLong = long.MaxValue;
            int expectedTotalSize = sizeof(long) + sizeof(int);

            int expectedSizeCalculation = sizeof(long);

            var pw = new PayloadWriter(shouldWriteBigEndian: false)
                .StartCalculatingSize("testSize1")
                    .Write(testLong)
                .EndSizeCalculation("testSize1");

            Assert.True(pw.TryWritePayload(out var payload));
            Assert.Equal(expectedTotalSize, payload.Length);

            var sr = new SequenceReader<byte>(payload);
            Assert.True(sr.Length == expectedTotalSize);

            Assert.True(sr.TryReadLittleEndian(out int totalCalculatedSize));
            Assert.Equal(expectedSizeCalculation, totalCalculatedSize);

            Assert.True(sr.TryReadLittleEndian(out long value));
            Assert.Equal(testLong, value);
        }

        [Fact]
        public void CanCalculateMultipleSizes()
        {
            long testLong1 = long.MaxValue;
            int testInt2 = int.MinValue;

            int expectedTotalSize1 =
                sizeof(int) // total size int
                + sizeof(long) // testLong1
                + sizeof(int) // partial size int
                + sizeof(int); // testInt2

            int expectedTestSize1 =
                sizeof(long)
                + sizeof(int)
                + sizeof(int);

            int expectedTestSize2 =
                sizeof(int); // testInt2

            var pw = new PayloadWriter(shouldWriteBigEndian: true)
                .StartCalculatingSize("testSize1")
                    .Write(testLong1)
                    .StartCalculatingSize("testSize2")
                        .Write(testInt2)
                    .EndSizeCalculation("testSize2")
                .EndSizeCalculation("testSize1");

            Assert.True(pw.TryWritePayload(out var payload));
            Assert.Equal(expectedTotalSize1, payload.Length);

            var sr = new SequenceReader<byte>(payload);
            Assert.True(sr.Length == expectedTotalSize1);

            Assert.True(sr.TryReadBigEndian(out int totalSize1));
            Assert.Equal(expectedTestSize1, totalSize1);

            Assert.True(sr.TryReadBigEndian(out long long1));
            Assert.Equal(testLong1, long1);

            Assert.True(sr.TryReadBigEndian(out int partialSize2));
            Assert.Equal(expectedTestSize2, partialSize2);

            Assert.True(sr.TryReadBigEndian(out int int2));
            Assert.Equal(testInt2, int2);
        }

        [Fact]
        public void UniqueNamesOnlyForSize()
        {
            Assert.Throws<ArgumentException>(() => new PayloadWriter(shouldWriteBigEndian: true)
                .StartCalculatingSize("testSize1")
                    .Write(1)
                .StartCalculatingSize("testSize1")
                .EndSizeCalculation("testSize1"));
        }

        [Fact]
        public void SizeCalculationsWorkAcrossChildren()
        {
            int testInt1 = 1337;
            long testLong2 = long.MaxValue;

            var expectedTotalSize1 =
                sizeof(int)
                + sizeof(int)
                + sizeof(long);

            var expectedCalculatedSize1 =
                sizeof(int)
                + sizeof(long);

            var pw1 = new PayloadWriter(shouldWriteBigEndian: true)
                .StartCalculatingSize("testSize1")
                .Write(testInt1);

            pw1.Context.CreatePayloadWriter()
                .Write(testLong2)
                .EndSizeCalculation("testSize1");

            Assert.True(pw1.TryWritePayload(out var payload));
            Assert.Equal(expectedTotalSize1, payload.Length);

            var sr = new SequenceReader<byte>(payload);
            Assert.True(sr.Length == expectedTotalSize1);

            Assert.True(sr.TryReadBigEndian(out int calculatedSize1));
            Assert.Equal(expectedCalculatedSize1, calculatedSize1);

            Assert.True(sr.TryReadBigEndian(out int int1));
            Assert.Equal(testInt1, int1);

            Assert.True(sr.TryReadBigEndian(out long long2));
            Assert.Equal(testLong2, long2);
        }

        [Fact]
        public void AllSizeCalculationsMustBeClosed()
        {
            var pw1 = new PayloadWriter(shouldWriteBigEndian: true)
                .StartCalculatingSize("testSize1")
                .Write(1);

            var context = pw1.Context;

            Assert.Throws<InvalidOperationException>(() => context.CreatePayloadWriter().TryWritePayload(out var _));
        }

        private class TestObject
        {
            public int testValue = -1;
        }

        [Fact]
        public void ArraysCanBeWrittenNullAndSizeCalculationWorks()
        {
            TestObject[] testObjectArray = null;
            // 8 = size value and the value itself
            int expectedTotalSize1 = 8;

            // 4 = just the value itself that the size calculation calculated.
            int expectedCalculatedSize1 = 4;

            var pw1 = new PayloadWriter(shouldWriteBigEndian: true)
                .StartCalculatingSize(nameof(ArraysCanBeWrittenNullAndSizeCalculationWorks))
                    .WriteArray(testObjectArray, TestObjectWriter)
                .EndSizeCalculation(nameof(ArraysCanBeWrittenNullAndSizeCalculationWorks));

            Assert.True(pw1.TryWritePayload(out var payload));
            Assert.Equal(expectedTotalSize1, payload.Length);

            var sr = new SequenceReader<byte>(payload);
            Assert.True(sr.Length == expectedTotalSize1);

            Assert.True(sr.TryReadBigEndian(out int calculatedSize1));
            Assert.Equal(expectedCalculatedSize1, calculatedSize1);

            Assert.True(sr.TryReadBigEndian(out int nullArraySize));
            Assert.Equal(-1, nullArraySize);
        }

        [Fact]
        public void ArraysCanBeWrittenAndSizeCalculationWorks()
        {
            TestObject[] testObjectArray = new TestObject[]
            {
                new TestObject
                {
                    testValue = 1337,
                },
                new TestObject
                {
                    testValue = 7331,
                },
            };

            // 8 = size value and the value itself
            int expectedTotalSize1 = 16;

            // 4 = just the value itself that the size calculation calculated.
            int expectedCalculatedSize1 = 12;

            var pw1 = new PayloadWriter(shouldWriteBigEndian: true)
                .StartCalculatingSize(nameof(ArraysCanBeWrittenNullAndSizeCalculationWorks))
                    .WriteArray(testObjectArray, TestObjectWriter)
                .EndSizeCalculation(nameof(ArraysCanBeWrittenNullAndSizeCalculationWorks));

            Assert.True(pw1.TryWritePayload(out var payload));
            Assert.Equal(expectedTotalSize1, payload.Length);

            var sr = new SequenceReader<byte>(payload);
            Assert.True(sr.Length == expectedTotalSize1);

            Assert.True(sr.TryReadBigEndian(out int calculatedSize1));
            Assert.Equal(expectedCalculatedSize1, calculatedSize1);

            Assert.True(sr.TryReadBigEndian(out int arraySize));
            Assert.Equal(testObjectArray.Length, arraySize);

            Assert.True(sr.TryReadBigEndian(out int elementValue0));
            Assert.Equal(testObjectArray[0].testValue, elementValue0);

            Assert.True(sr.TryReadBigEndian(out int elementValue1));
            Assert.Equal(testObjectArray[1].testValue, elementValue1);
        }

        private PayloadWriterContext TestObjectWriter(TestObject testObject, PayloadWriterContext context)
        {
            var pw = context.CreatePayloadWriter();

            pw.Write(testObject.testValue);

            return pw.Context;
        }
    }
}
