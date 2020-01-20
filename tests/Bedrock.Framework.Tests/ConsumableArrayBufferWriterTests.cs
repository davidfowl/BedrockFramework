using Bedrock.Framework.Protocols;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Xunit;

namespace Bedrock.Framework.Tests
{
    public abstract class ConsumableArrayBufferWriterTests<T> where T : IEquatable<T>
    {
        [Fact]
        public void ThrowsIfConsumeIsLessThanZero()
        {
            var output = new ConsumableArrayBufferWriter<T>();
            WriteData(output, 1);
            Assert.Throws<ArgumentException>(() => output.Consume(-1));
        }

        [Fact]
        public void ThrowsIfTotalConsumedIsGreaterThanWritten()
        {
            var output = new ConsumableArrayBufferWriter<T>();
            Assert.Throws<InvalidOperationException>(() => output.Consume(1));
            WriteData(output, 2);
            output.Consume(1);
            output.Consume(1);
            Assert.Throws<InvalidOperationException>(() => output.Consume(1));
            WriteData(output, 2);
            Assert.Throws<InvalidOperationException>(() => output.Consume(3));
        }

        [Fact]
        public void ThrowsIfConsumePlusConsumedWouldOverflow()
        {
            var output = new ConsumableArrayBufferWriter<T>(256);
            WriteData(output, 100);
            output.Consume(50);
            Assert.Throws<InvalidOperationException>(() => output.Consume(int.MaxValue));
        }

        [Fact]
        public void IfConsumedEqualsWrittenFreeCapacityIsReset()
        {
            var output = new ConsumableArrayBufferWriter<T>(256);
            Assert.Equal(256, output.FreeCapacity);
            WriteData(output, 128);
            Assert.Equal(128, output.FreeCapacity);
            output.Consume(64);
            Assert.Equal(128, output.FreeCapacity);
            output.Consume(64);
            Assert.Equal(256, output.FreeCapacity);
        }

        [Fact]
        public void ConsumeReducesUnconsumedWrittenCount()
        {
            var output = new ConsumableArrayBufferWriter<T>();
            WriteData(output, 2);
            Assert.Equal(2, output.UnconsumedWrittenCount);
            output.Consume(1);
            Assert.Equal(1, output.UnconsumedWrittenCount);
        }

        [Fact]
        public void ConsumeIsNotIncludedInWrittenSpanOrMemory()
        {
            var output = new ConsumableArrayBufferWriter<T>();
            WriteData(output, 2);
            var oldSpan = output.WrittenSpan;
            var oldMemory = output.WrittenMemory;
            Assert.Equal(2, oldSpan.Length);
            Assert.Equal(2, oldMemory.Length);
            output.Consume(1);
            var newSpan = output.WrittenSpan;
            var newMemory = output.WrittenMemory;
            Assert.Equal(1, newSpan.Length);
            Assert.Equal(1, newMemory.Length);
            Assert.True(oldSpan[1..].SequenceEqual(newSpan));
            Assert.True(oldMemory[1..].Equals(newMemory));
        }

        [Fact]
        public void CapacityDoesNotIncreaseIfThereIsPlentyOfConsumedSpace()
        {
            var output = new ConsumableArrayBufferWriter<T>(256);
            var capacity = output.Capacity;
            WriteData(output, capacity);
            output.Consume(capacity - 100);
            var data = output.WrittenSpan.ToArray();
            output.GetMemory(16);
            Assert.Equal(capacity, output.Capacity);
            Assert.Equal(data, output.WrittenSpan.ToArray());
            Assert.Equal(capacity - 100, output.FreeCapacity);
            Assert.Equal(100, output.UnconsumedWrittenCount);
        }

        [Fact]
        public void CapacityDoesIncreaseIfThereIsOnlyMinimalConsumedSpace()
        {
            var output = new ConsumableArrayBufferWriter<T>(256);
            var capacity = output.Capacity;
            WriteData(output, capacity);
            output.Consume(100);
            var data = output.WrittenSpan.ToArray();
            output.GetMemory(16);
            Assert.InRange(output.Capacity, capacity * 2, int.MaxValue);
            Assert.Equal(data, output.WrittenSpan.ToArray());
            Assert.Equal(capacity - 100, output.UnconsumedWrittenCount);
        }

        [Fact]
        public void ThrowsIfUsedAfterDisposed()
        {
            var buffer = new ConsumableArrayBufferWriter<T>();
            buffer.Dispose();
            Assert.Throws<NullReferenceException>(() => buffer.Clear());
            Assert.Throws<NullReferenceException>(() => buffer.GetMemory());
            Assert.Throws<NullReferenceException>(() => buffer.GetSpan());
        }

        #region ArrayBufferWriterTests
        [Fact]
        public void ArrayBufferWriter_Ctor()
        {
            {
                var output = new ConsumableArrayBufferWriter<T>();
                Assert.Equal(0, output.FreeCapacity);
                Assert.Equal(0, output.Capacity);
                Assert.Equal(0, output.UnconsumedWrittenCount);
                Assert.True(ReadOnlySpan<T>.Empty.SequenceEqual(output.WrittenSpan));
                Assert.True(ReadOnlyMemory<T>.Empty.Span.SequenceEqual(output.WrittenMemory.Span));
            }

            {
                var output = new ConsumableArrayBufferWriter<T>(200);
                Assert.True(output.FreeCapacity >= 200);
                Assert.True(output.Capacity >= 200);
                Assert.Equal(0, output.UnconsumedWrittenCount);
                Assert.True(ReadOnlySpan<T>.Empty.SequenceEqual(output.WrittenSpan));
                Assert.True(ReadOnlyMemory<T>.Empty.Span.SequenceEqual(output.WrittenMemory.Span));
            }

            {
                ConsumableArrayBufferWriter<T> output = default;
                Assert.Null(output);
            }
        }

        [Fact]
        public void Invalid_Ctor()
        {
            Assert.Throws<ArgumentException>(() => new ConsumableArrayBufferWriter<T>(0));
            Assert.Throws<ArgumentException>(() => new ConsumableArrayBufferWriter<T>(-1));
            Assert.Throws<OutOfMemoryException>(() => new ConsumableArrayBufferWriter<T>(int.MaxValue));
        }

        [Fact]
        public void Clear()
        {
            var output = new ConsumableArrayBufferWriter<T>(256);
            var previousAvailable = output.FreeCapacity;
            WriteData(output, 2);
            Assert.True(output.FreeCapacity < previousAvailable);
            Assert.True(output.UnconsumedWrittenCount > 0);
            Assert.False(ReadOnlySpan<T>.Empty.SequenceEqual(output.WrittenSpan));
            Assert.False(ReadOnlyMemory<T>.Empty.Span.SequenceEqual(output.WrittenMemory.Span));
            Assert.True(output.WrittenSpan.SequenceEqual(output.WrittenMemory.Span));
            output.Clear();
            Assert.Equal(0, output.UnconsumedWrittenCount);
            Assert.True(ReadOnlySpan<T>.Empty.SequenceEqual(output.WrittenSpan));
            Assert.True(ReadOnlyMemory<T>.Empty.Span.SequenceEqual(output.WrittenMemory.Span));
            Assert.Equal(previousAvailable, output.FreeCapacity);
            Assert.Equal(0, output.UnconsumedWrittenCount);
        }

        [Fact]
        public void Advance()
        {
            {
                var output = new ConsumableArrayBufferWriter<T>();
                var capacity = output.Capacity;
                Assert.Equal(capacity, output.FreeCapacity);
                output.Advance(output.FreeCapacity);
                Assert.Equal(capacity, output.UnconsumedWrittenCount);
                Assert.Equal(0, output.FreeCapacity);
            }

            {
                var output = new ConsumableArrayBufferWriter<T>();
                output.Advance(output.Capacity);
                Assert.Equal(output.Capacity, output.UnconsumedWrittenCount);
                Assert.Equal(0, output.FreeCapacity);
                var previousCapacity = output.Capacity;
                var _ = output.GetSpan();
                Assert.True(output.Capacity > previousCapacity);
            }

            {
                var output = new ConsumableArrayBufferWriter<T>(256);
                WriteData(output, 2);
                var previousMemory = output.WrittenMemory;
                var previousSpan = output.WrittenSpan;
                Assert.True(previousSpan.SequenceEqual(previousMemory.Span));
                output.Advance(10);
                Assert.False(previousMemory.Span.SequenceEqual(output.WrittenMemory.Span));
                Assert.False(previousSpan.SequenceEqual(output.WrittenSpan));
                Assert.True(output.WrittenSpan.SequenceEqual(output.WrittenMemory.Span));
            }

            {
                var output = new ConsumableArrayBufferWriter<T>();
                _ = output.GetSpan(20);
                WriteData(output, 10);
                var previousMemory = output.WrittenMemory;
                var previousSpan = output.WrittenSpan;
                Assert.True(previousSpan.SequenceEqual(previousMemory.Span));
                Assert.Throws<InvalidOperationException>(() => output.Advance(247));
                output.Advance(10);
                Assert.False(previousMemory.Span.SequenceEqual(output.WrittenMemory.Span));
                Assert.False(previousSpan.SequenceEqual(output.WrittenSpan));
                Assert.True(output.WrittenSpan.SequenceEqual(output.WrittenMemory.Span));
            }
        }

        [Fact]
        public void AdvanceZero()
        {
            var output = new ConsumableArrayBufferWriter<T>();
            WriteData(output, 2);
            Assert.Equal(2, output.UnconsumedWrittenCount);
            var previousMemory = output.WrittenMemory;
            var previousSpan = output.WrittenSpan;
            Assert.True(previousSpan.SequenceEqual(previousMemory.Span));
            output.Advance(0);
            Assert.Equal(2, output.UnconsumedWrittenCount);
            Assert.True(previousMemory.Span.SequenceEqual(output.WrittenMemory.Span));
            Assert.True(previousSpan.SequenceEqual(output.WrittenSpan));
            Assert.True(output.WrittenSpan.SequenceEqual(output.WrittenMemory.Span));
        }

        [Fact]
        public void InvalidAdvance()
        {
            {
                var output = new ConsumableArrayBufferWriter<T>();
                Assert.Throws<ArgumentException>(() => output.Advance(-1));
                Assert.Throws<InvalidOperationException>(() => output.Advance(output.Capacity + 1));
            }

            {
                var output = new ConsumableArrayBufferWriter<T>();
                WriteData(output, 100);
                Assert.Throws<InvalidOperationException>(() => output.Advance(output.FreeCapacity + 1));
            }
        }

        [Fact]
        public void GetSpan_DefaultCtor()
        {
            var output = new ConsumableArrayBufferWriter<T>();
            var span = output.GetSpan();
            Assert.Equal(256, span.Length);
        }

        [Theory]
        [MemberData(nameof(SizeHints))]
        public void GetSpanWithSizeHint_DefaultCtor(int sizeHint)
        {
            var output = new ConsumableArrayBufferWriter<T>();
            var span = output.GetSpan(sizeHint);
            Assert.InRange(span.Length, sizeHint, int.MaxValue);
        }

        [Fact]
        public void GetSpan_InitSizeCtor()
        {
            var output = new ConsumableArrayBufferWriter<T>(100);
            var span = output.GetSpan();
            Assert.InRange(span.Length, 100, int.MaxValue);
        }

        [Theory]
        [MemberData(nameof(SizeHints))]
        public void GetSpanWithSizeHint_InitSizeCtor(int sizeHint)
        {
            {
                var output = new ConsumableArrayBufferWriter<T>(256);
                var span = output.GetSpan(sizeHint);
                Assert.InRange(span.Length, sizeHint, int.MaxValue);
            }

            {
                var output = new ConsumableArrayBufferWriter<T>(1000);
                var span = output.GetSpan(sizeHint);
                Assert.InRange(span.Length, sizeHint <= 1000 ? 1000 : sizeHint + 1000, int.MaxValue);
            }
        }

        [Fact]
        public void GetMemory_DefaultCtor()
        {
            var output = new ConsumableArrayBufferWriter<T>();
            var memory = output.GetMemory();
            Assert.Equal(256, memory.Length);
        }

        [Theory]
        [MemberData(nameof(SizeHints))]
        public void GetMemoryWithSizeHint_DefaultCtor(int sizeHint)
        {
            var output = new ConsumableArrayBufferWriter<T>();
            var memory = output.GetMemory(sizeHint);
            Assert.InRange(memory.Length, sizeHint, int.MaxValue);
        }

        [Fact]
        public void GetMemory_InitSizeCtor()
        {
            var output = new ConsumableArrayBufferWriter<T>(100);
            var memory = output.GetMemory();
            Assert.InRange(memory.Length, 100, int.MaxValue);
        }

        [Theory]
        [MemberData(nameof(SizeHints))]
        public void GetMemoryWithSizeHint_InitSizeCtor(int sizeHint)
        {
            {
                var output = new ConsumableArrayBufferWriter<T>(256);
                var memory = output.GetMemory(sizeHint);
                Assert.InRange(memory.Length, sizeHint, int.MaxValue);
            }

            {
                var output = new ConsumableArrayBufferWriter<T>(1000);
                var memory = output.GetMemory(sizeHint);
                Assert.InRange(memory.Length, sizeHint <= 1000 ? 1000 : sizeHint + 1000, int.MaxValue);
            }
        }

        public static bool IsX64 { get; } = IntPtr.Size == 8;

        [Fact]
        public void GetMemoryAndSpan()
        {
            {
                var output = new ConsumableArrayBufferWriter<T>();
                WriteData(output, 2);
                var span = output.GetSpan();
                var memory = output.GetMemory();
                var memorySpan = memory.Span;
                Assert.True(span.Length > 0);
                Assert.True(memorySpan.Length > 0);
                Assert.Equal(span.Length, memorySpan.Length);
            }

            {
                var output = new ConsumableArrayBufferWriter<T>();
                WriteData(output, 2);
                var writtenSoFarMemory = output.WrittenMemory;
                var writtenSoFar = output.WrittenSpan;
                Assert.True(writtenSoFarMemory.Span.SequenceEqual(writtenSoFar));
                var span = output.GetSpan(500);
                Assert.True(span.Length >= 500);
                Assert.True(output.FreeCapacity >= 500);

                Assert.Equal(writtenSoFar.Length, output.UnconsumedWrittenCount);

                var memory = output.GetMemory();
                var memorySpan = memory.Span;
                Assert.True(span.Length >= 500);
                Assert.True(memorySpan.Length >= 500);
                Assert.Equal(span.Length, memorySpan.Length);

                memory = output.GetMemory(500);
                memorySpan = memory.Span;
                Assert.True(memorySpan.Length >= 500);
                Assert.Equal(span.Length, memorySpan.Length);
            }
        }

        [Fact]
        public void GetSpanShouldAtleastDoubleWhenGrowing()
        {
            var output = new ConsumableArrayBufferWriter<T>(256);
            WriteData(output, 100);
            var previousAvailable = output.FreeCapacity;

            _ = output.GetSpan(previousAvailable);
            Assert.Equal(previousAvailable, output.FreeCapacity);

            _ = output.GetSpan(previousAvailable + 1);
            Assert.True(output.FreeCapacity >= previousAvailable * 2);
        }

        [Fact]
        public void GetSpanOnlyGrowsAboveThreshold()
        {
            {
                var output = new ConsumableArrayBufferWriter<T>();
                _ = output.GetSpan();
                var previousAvailable = output.FreeCapacity;

                for (var i = 0; i < 10; i++)
                {
                    _ = output.GetSpan();
                    Assert.Equal(previousAvailable, output.FreeCapacity);
                }
            }

            {
                var output = new ConsumableArrayBufferWriter<T>();
                _ = output.GetSpan(10);
                var previousAvailable = output.FreeCapacity;

                for (var i = 0; i < 10; i++)
                {
                    _ = output.GetSpan(previousAvailable);
                    Assert.Equal(previousAvailable, output.FreeCapacity);
                }
            }
        }

        [Fact]
        public void InvalidGetMemoryAndSpan()
        {
            var output = new ConsumableArrayBufferWriter<T>();
            WriteData(output, 2);
            Assert.Throws<ArgumentException>(() => output.GetSpan(-1));
            Assert.Throws<ArgumentException>(() => output.GetMemory(-1));
        }

        protected abstract void WriteData(IBufferWriter<T> bufferWriter, int numBytes);

        public static IEnumerable<object[]> SizeHints
        {
            get
            {
                return new List<object[]>
                {
                    new object[] { 0 },
                    new object[] { 1 },
                    new object[] { 2 },
                    new object[] { 3 },
                    new object[] { 99 },
                    new object[] { 100 },
                    new object[] { 101 },
                    new object[] { 255 },
                    new object[] { 256 },
                    new object[] { 257 },
                    new object[] { 1000 },
                    new object[] { 2000 },
                };
            }
        }
        #endregion
    }

    public class ConsumableArrayBufferWriterTests_Byte : ConsumableArrayBufferWriterTests<byte>
    {
        protected override void WriteData(IBufferWriter<byte> bufferWriter, int numBytes)
        {
            var outputSpan = bufferWriter.GetSpan(numBytes);
            Assert.True(outputSpan.Length >= numBytes);
            var random = new Random(42);

            var data = new byte[numBytes];
            random.NextBytes(data);
            data.CopyTo(outputSpan);

            bufferWriter.Advance(numBytes);
        }
    }

    public class ConsumableArrayBufferWriterTests_String : ConsumableArrayBufferWriterTests<string>
    {
        protected override void WriteData(IBufferWriter<string> bufferWriter, int numStrings)
        {
            var outputSpan = bufferWriter.GetSpan(numStrings);
            Debug.Assert(outputSpan.Length >= numStrings);
            var random = new Random(42);

            var data = new string[numStrings];

            for (var i = 0; i < numStrings; i++)
            {
                var length = random.Next(5, 10);
                data[i] = GetRandomString(random, length, 32, 127);
            }

            data.CopyTo(outputSpan);

            bufferWriter.Advance(numStrings);
        }
        private static string GetRandomString(Random r, int length, int minCodePoint, int maxCodePoint)
        {
            var sb = new StringBuilder(length);
            while (length-- != 0)
            {
                sb.Append((char)r.Next(minCodePoint, maxCodePoint));
            }
            return sb.ToString();
        }
    }
}
