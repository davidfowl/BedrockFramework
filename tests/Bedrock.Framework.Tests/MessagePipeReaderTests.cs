using Bedrock.Framework.Protocols;
using Microsoft.AspNetCore.Connections;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Bedrock.Framework.Tests
{
    public class MessagePipeReaderTests
    {
        private static MessagePipeReader CreateReader(out Func<byte[], Task> writeFunc)
        {
            var protocol = new TestProtocol();
            var stream = new MemoryStream();
            var writer = PipeWriter.Create(stream);
            var reader = new MessagePipeReader(PipeReader.Create(stream), protocol);

            long written = 0;
            writeFunc = async bytes =>
            {
                var position = stream.Position;
                stream.Position = written;
                protocol.WriteMessage(bytes, writer);
                await writer.FlushAsync().ConfigureAwait(false);
                written = stream.Position;
                stream.Position = position;
            };
            return reader;
        }

        private static async Task<MessagePipeReader> CreateReaderOverBytes(byte[] bytes)
        {
            var reader = CreateReader(out var writeFunc);
            await writeFunc(bytes).ConfigureAwait(false);
            return reader;
        }

        [Fact]
        public async Task CanRead()
        {
            var reader = await CreateReaderOverBytes(Encoding.ASCII.GetBytes("Hello World")).ConfigureAwait(false);

            var readResult = await reader.ReadAsync();
            var buffer = readResult.Buffer;

            Assert.Equal(11, buffer.Length);
            Assert.True(buffer.IsSingleSegment);
            Assert.Equal("Hello World", Encoding.ASCII.GetString(buffer.ToArray()));

            reader.AdvanceTo(buffer.End);
            reader.Complete();
        }

        [Fact]
        public async Task ReadAsyncReturnsFullBacklogWhenNotFullyConsumed()
        {
            var reader = CreateReader(out var writeFunc);
            await writeFunc(Encoding.ASCII.GetBytes("Hello ")).ConfigureAwait(false);
            await writeFunc(Encoding.ASCII.GetBytes("World")).ConfigureAwait(false);

            var readResult = await reader.ReadAsync();
            var buffer = readResult.Buffer;

            Assert.Equal(6, buffer.Length);
            Assert.True(buffer.IsSingleSegment);
            Assert.Equal("Hello ", Encoding.ASCII.GetString(buffer.ToArray()));

            reader.AdvanceTo(buffer.GetPosition(3));
            readResult = await reader.ReadAsync();
            buffer = readResult.Buffer;

            Assert.Equal(8, buffer.Length);
            Assert.True(buffer.IsSingleSegment);
            Assert.Equal("lo World", Encoding.ASCII.GetString(buffer.ToArray()));

            await writeFunc(Encoding.ASCII.GetBytes("\nLorem Ipsum")).ConfigureAwait(false);
            reader.AdvanceTo(buffer.GetPosition(2));
            readResult = await reader.ReadAsync();
            buffer = readResult.Buffer;

            Assert.Equal(18, buffer.Length);
            Assert.True(buffer.IsSingleSegment);
            Assert.Equal(" World\nLorem Ipsum", Encoding.ASCII.GetString(buffer.ToArray()));

            reader.Complete();
        }

        [Fact]
        public async Task TryReadReturnsTrueIfBufferedBytesAndNotExaminedEverything()
        {
            var reader = await CreateReaderOverBytes(Encoding.ASCII.GetBytes("Hello World")).ConfigureAwait(false);

            var readResult = await reader.ReadAsync();
            var buffer = readResult.Buffer;
            Assert.Equal(11, buffer.Length);
            Assert.True(buffer.IsSingleSegment);
            reader.AdvanceTo(buffer.Start, buffer.GetPosition(5));

            Assert.True(reader.TryRead(out readResult));
            buffer = readResult.Buffer;
            Assert.Equal(11, buffer.Length);
            Assert.True(buffer.IsSingleSegment);
            Assert.Equal("Hello World", Encoding.ASCII.GetString(buffer.ToArray()));

            reader.Complete();
        }

        [Fact]
        public async Task TryReadReturnsFalseIfBufferedBytesAndEverythingConsumed()
        {
            var reader = await CreateReaderOverBytes(Encoding.ASCII.GetBytes("Hello World")).ConfigureAwait(false);

            var readResult = await reader.ReadAsync();
            var buffer = readResult.Buffer;
            Assert.Equal(11, buffer.Length);
            Assert.True(buffer.IsSingleSegment);
            reader.AdvanceTo(buffer.End);

            Assert.False(reader.TryRead(out readResult));
            reader.Complete();
        }

        [Fact]
        public async Task TryReadReturnsFalseIfBufferedBytesAndEverythingExamined()
        {
            var reader = await CreateReaderOverBytes(Encoding.ASCII.GetBytes("Hello World")).ConfigureAwait(false);

            var readResult = await reader.ReadAsync();
            var buffer = readResult.Buffer;
            Assert.Equal(11, buffer.Length);
            Assert.True(buffer.IsSingleSegment);
            reader.AdvanceTo(buffer.Start, buffer.End);

            Assert.False(reader.TryRead(out readResult));
            reader.Complete();
        }

        [Fact]
        public async Task TryReadReturnsTrueIfBufferedBytesAndEverythingExaminedButMoreDataSynchronouslyAvailabe()
        {
            var reader = CreateReader(out var writeFunc);
            await writeFunc(Encoding.ASCII.GetBytes("Hello ")).ConfigureAwait(false);
            await writeFunc(Encoding.ASCII.GetBytes("World")).ConfigureAwait(false);

            var readResult = await reader.ReadAsync();
            var buffer = readResult.Buffer;
            Assert.Equal(6, buffer.Length);
            Assert.True(buffer.IsSingleSegment);
            reader.AdvanceTo(buffer.Start, buffer.End);

            Assert.True(reader.TryRead(out readResult));
            buffer = readResult.Buffer;
            Assert.Equal(11, buffer.Length);
            Assert.True(buffer.IsSingleSegment);
            Assert.Equal("Hello World", Encoding.ASCII.GetString(buffer.ToArray()));

            reader.Complete();
        }

        [Fact]
        public async Task TryReadReturnsTrueIfBufferedBytesAndEverythingConsumedButMoreDataSynchronouslyAvailabe()
        {
            var reader = CreateReader(out var writeFunc);
            await writeFunc(Encoding.ASCII.GetBytes("Hello ")).ConfigureAwait(false);
            await writeFunc(Encoding.ASCII.GetBytes("World")).ConfigureAwait(false);

            var readResult = await reader.ReadAsync();
            var buffer = readResult.Buffer;
            Assert.Equal(6, buffer.Length);
            Assert.True(buffer.IsSingleSegment);
            reader.AdvanceTo(buffer.End);

            Assert.True(reader.TryRead(out readResult));
            buffer = readResult.Buffer;
            Assert.Equal(5, buffer.Length);
            Assert.True(buffer.IsSingleSegment);
            Assert.Equal("World", Encoding.ASCII.GetString(buffer.ToArray()));

            reader.Complete();
        }

        [Fact]
        public async Task CanReadMultipleTimes()
        {
            // This needs to run inline to synchronize the reader and writer
            TaskCompletionSource<object> waitForRead = null;
            var protocol = new TestProtocol();

            async Task DoAsyncRead(PipeReader reader, int[] bufferSizes)
            {
                var index = 0;
                while (true)
                {
                    var readResult = await reader.ReadAsync().ConfigureAwait(false);

                    if (readResult.IsCompleted)
                    {
                        break;
                    }

                    Assert.Equal(bufferSizes[index], readResult.Buffer.Length);
                    reader.AdvanceTo(readResult.Buffer.End);
                    index++;
                    waitForRead?.TrySetResult(null);
                }

                reader.Complete();
            }

            async Task DoAsyncWrites(PipeWriter writer, int[] bufferSizes)
            {
                for (var i = 0; i < bufferSizes.Length; i++)
                {
                    writer.WriteEmpty(protocol, bufferSizes[i]);
                    waitForRead = new TaskCompletionSource<object>();
                    await writer.FlushAsync().ConfigureAwait(false);
                    await waitForRead.Task;
                }

                writer.Complete();
            }

            // We're using the pipe here as a way to pump bytes into the reader asynchronously
            var pipe = new Pipe();
            var options = new StreamPipeReaderOptions(bufferSize: 4096);
            var reader = new MessagePipeReader(PipeReader.Create(pipe.Reader.AsStream(), options), protocol);

            var writes = new[] { 4096, 1024, 123, 4096, 100 };

            var readingTask = DoAsyncRead(reader, writes);
            var writingTask = DoAsyncWrites(pipe.Writer, writes);

            await readingTask;
            await writingTask;

            pipe.Reader.Complete();
        }

        [Fact]
        public async Task CanConsumeAllBytes()
        {
            var reader = await CreateReaderOverBytes(new byte[100]).ConfigureAwait(false);
            var buffer = (await reader.ReadAsync()).Buffer;

            reader.AdvanceTo(buffer.End);

            reader.Complete();
        }

        [Fact]
        public async Task CanConsumeNoBytes()
        {
            var reader = await CreateReaderOverBytes(new byte[100]).ConfigureAwait(false);
            var buffer = (await reader.ReadAsync()).Buffer;

            reader.AdvanceTo(buffer.Start);

            reader.Complete();
        }

        [Fact]
        public async Task CanExamineAllBytes()
        {
            var reader = await CreateReaderOverBytes(new byte[100]).ConfigureAwait(false);
            var buffer = (await reader.ReadAsync()).Buffer;

            reader.AdvanceTo(buffer.Start, buffer.End);

            reader.Complete();
        }

        [Fact]
        public async Task CanExamineNoBytes()
        {
            var reader = await CreateReaderOverBytes(new byte[100]).ConfigureAwait(false);
            var buffer = (await reader.ReadAsync()).Buffer;

            reader.AdvanceTo(buffer.Start, buffer.Start);

            reader.Complete();
        }

        [Fact]
        public async Task ReadAsyncAfterReceivingCompletedReadResultDoesNotThrow()
        {
            var stream = new ThrowAfterZeroByteReadStream();
            var reader = new MessagePipeReader(PipeReader.Create(stream), new TestProtocol());
            var readResult = await reader.ReadAsync();
            Assert.True(readResult.Buffer.IsEmpty);
            Assert.True(readResult.IsCompleted);
            reader.AdvanceTo(readResult.Buffer.End);

            readResult = await reader.ReadAsync();
            Assert.True(readResult.Buffer.IsEmpty);
            Assert.True(readResult.IsCompleted);
            reader.AdvanceTo(readResult.Buffer.End);
            reader.Complete();
        }

        [Fact]
        public async Task BufferingDataPastEndOfStreamCanBeReadAgain()
        {
            var protocol = new TestProtocol();
            var stream = new ThrowAfterZeroByteReadStream();
            var writer = PipeWriter.Create(stream);
            var reader = new MessagePipeReader(PipeReader.Create(stream), protocol);

            protocol.WriteMessage(Encoding.ASCII.GetBytes("Hello World"), writer);
            await writer.FlushAsync().ConfigureAwait(false);
            stream.Position = 0;

            var readResult = await reader.ReadAsync();
            var buffer = readResult.Buffer;
            reader.AdvanceTo(buffer.Start, buffer.End);

            // Make sure IsCompleted is true
            readResult = await reader.ReadAsync();
            buffer = readResult.Buffer;
            reader.AdvanceTo(buffer.Start, buffer.End);
            Assert.True(readResult.IsCompleted);

            var value = await ReadFromPipeAsString(reader);
            Assert.Equal("Hello World", value);
            reader.Complete();
        }

        [Fact]
        public async Task NextReadAfterPartiallyExaminedReturnsImmediately()
        {
            // TODO: decide on behaviour here, and write tests
        }

        [Fact]
        public async Task CompleteReaderWithoutAdvanceDoesNotThrow()
        {
            var reader = new MessagePipeReader(PipeReader.Create(Stream.Null), new TestProtocol());
            await reader.ReadAsync();
            reader.Complete();
        }

        [Fact]
        public async Task AdvanceAfterCompleteThrows()
        {
            var reader = await CreateReaderOverBytes(new byte[100]).ConfigureAwait(false);
            var buffer = (await reader.ReadAsync()).Buffer;

            reader.Complete();

            Assert.Throws<InvalidOperationException>(() => reader.AdvanceTo(buffer.End));
        }

        [Fact]
        public async Task ThrowsOnReadAfterCompleteReader()
        {
            var reader = new MessagePipeReader(PipeReader.Create(Stream.Null), new TestProtocol());

            reader.Complete();
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await reader.ReadAsync());
        }

        [Fact]
        public void TryReadAfterCompleteThrows()
        {
            var reader = new MessagePipeReader(PipeReader.Create(Stream.Null), new TestProtocol());

            reader.Complete();
            Assert.Throws<InvalidOperationException>(() => reader.TryRead(out _));
        }

        [Fact]
        public void TryReadAfterCancelPendingReadReturnsTrue()
        {
            var reader = new MessagePipeReader(PipeReader.Create(Stream.Null), new TestProtocol());

            reader.CancelPendingRead();

            Assert.True(reader.TryRead(out var result));
            Assert.True(result.IsCanceled);
            reader.AdvanceTo(result.Buffer.End);
            reader.Complete();
        }

        [Fact]
        public async Task ReadCanBeCancelledViaProvidedCancellationToken()
        {
            var stream = new CancelledReadsStream();
            PipeReader reader = new MessagePipeReader(PipeReader.Create(stream), new TestProtocol());
            var cts = new CancellationTokenSource();

            var task = reader.ReadAsync(cts.Token);

            Assert.False(task.IsCompleted);

            cts.Cancel();

            stream.WaitForReadTask.TrySetResult(null);

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await task);
            reader.Complete();
        }

        [Fact]
        public async Task ReadCanBeCanceledViaCancelPendingReadWhenReadIsAsync()
        {
            var stream = new CancelledReadsStream();
            PipeReader reader = new MessagePipeReader(PipeReader.Create(stream), new TestProtocol());

            var task = reader.ReadAsync();

            reader.CancelPendingRead();

            stream.WaitForReadTask.TrySetResult(null);

            var readResult = await task;
            Assert.True(readResult.IsCanceled);
            reader.Complete();
        }

        [Fact]
        public async Task ReadAsyncReturnsCanceledIfCanceledBeforeRead()
        {
            var reader = await CreateReaderOverBytes(new byte[10000]).ConfigureAwait(false);

            // Make sure state isn't used from before
            for (var i = 0; i < 3; i++)
            {
                reader.CancelPendingRead();
                var readResultTask = reader.ReadAsync();
                Assert.True(readResultTask.IsCompleted);
                var readResult = readResultTask.GetAwaiter().GetResult();
                Assert.True(readResult.IsCanceled);
                readResult = await reader.ReadAsync();
                reader.AdvanceTo(readResult.Buffer.End);
            }

            reader.Complete();
        }

        [Fact]
        public async Task ReadAsyncReturnsCanceledInterleaved()
        {
            var reader = await CreateReaderOverBytes(new byte[10000]).ConfigureAwait(false);

            // Cancel and Read interleaved to confirm cancellations are independent
            for (var i = 0; i < 3; i++)
            {
                reader.CancelPendingRead();
                var readResultTask = reader.ReadAsync();
                Assert.True(readResultTask.IsCompleted);
                var readResult = readResultTask.GetAwaiter().GetResult();
                Assert.True(readResult.IsCanceled);

                readResult = await reader.ReadAsync();
                Assert.False(readResult.IsCanceled);
            }

            reader.Complete();
        }

        [Fact]
        public async Task ConsumePartialBufferWorks()
        {
            var protocol = new TestProtocol();
            // We're using the pipe here as a way to pump bytes into the reader asynchronously
            var pipe = new Pipe();
            var reader = new MessagePipeReader(PipeReader.Create(pipe.Reader.AsStream()), protocol);

            pipe.Writer.WriteEmpty(protocol, 10);
            await pipe.Writer.FlushAsync();

            var readResult = await reader.ReadAsync();
            Assert.Equal(10, readResult.Buffer.Length);
            reader.AdvanceTo(readResult.Buffer.GetPosition(4), readResult.Buffer.End);

            pipe.Writer.WriteEmpty(protocol, 2);
            await pipe.Writer.FlushAsync();

            readResult = await reader.ReadAsync();
            // 6 bytes left over plus 2 newly written bytes
            Assert.Equal(8, readResult.Buffer.Length);
            reader.AdvanceTo(readResult.Buffer.End);

            reader.Complete();

            pipe.Writer.Complete();
            pipe.Reader.Complete();
        }

        #region pooling tests
        // TODO: Implement pooling

        //        [Fact]
        //        public async Task ConsumingSegmentsReturnsMemoryToPool()
        //        {
        //            using (var pool = new DisposeTrackingBufferPool())
        //            {
        //                var options = new StreamPipeReaderOptions(pool: pool, bufferSize: 4096, minimumReadSize: 1024);
        //                // 2 full segments
        //                var stream = new MemoryStream(new byte[options.BufferSize * 2]);
        //                var reader = PipeReader.Create(stream, options);

        //                var readResult = await reader.ReadAsync();
        //                var buffer = readResult.Buffer;
        //                Assert.Equal(1, pool.CurrentlyRentedBlocks);
        //                Assert.Equal(options.BufferSize, buffer.Length);
        //                reader.AdvanceTo(buffer.Start, buffer.End);

        //                readResult = await reader.ReadAsync();
        //                buffer = readResult.Buffer;
        //                Assert.Equal(options.BufferSize * 2, buffer.Length);
        //                Assert.Equal(2, pool.CurrentlyRentedBlocks);
        //                reader.AdvanceTo(buffer.Start, buffer.End);

        //                readResult = await reader.ReadAsync();
        //                buffer = readResult.Buffer;
        //                Assert.Equal(options.BufferSize * 2, buffer.Length);
        //                // We end up allocating a 3rd block here since we don't know ahead of time that
        //                // it's the last one
        //                Assert.Equal(3, pool.CurrentlyRentedBlocks);

        //                reader.AdvanceTo(buffer.Slice(buffer.Start, 4096).End, buffer.End);

        //                Assert.Equal(2, pool.CurrentlyRentedBlocks);
        //                Assert.Equal(1, pool.DisposedBlocks);

        //                readResult = await reader.ReadAsync();
        //                buffer = readResult.Buffer;
        //                Assert.Equal(options.BufferSize, buffer.Length);
        //                reader.AdvanceTo(buffer.Slice(buffer.Start, 4096).End, buffer.End);

        //                // All of the blocks get returned here since we hit the first case of emptying the entire list
        //                Assert.Equal(0, pool.CurrentlyRentedBlocks);
        //                Assert.Equal(3, pool.DisposedBlocks);

        //                reader.Complete();
        //            }
        //        }

        //        [Fact]
        //        public async Task CompletingReturnsUnconsumedMemoryToPool()
        //        {
        //            using (var pool = new DisposeTrackingBufferPool())
        //            {
        //                var options = new StreamPipeReaderOptions(pool: pool, bufferSize: 4096, minimumReadSize: 1024);
        //                // 2 full segments
        //                var stream = new MemoryStream(new byte[options.BufferSize * 3]);
        //                var reader = PipeReader.Create(stream, options);

        //                while (true)
        //                {
        //                    var readResult = await reader.ReadAsync();
        //                    reader.AdvanceTo(readResult.Buffer.Start, readResult.Buffer.End);

        //                    if (readResult.IsCompleted)
        //                    {
        //                        break;
        //                    }
        //                }

        //                Assert.Equal(4, pool.CurrentlyRentedBlocks);
        //                reader.Complete();
        //                Assert.Equal(0, pool.CurrentlyRentedBlocks);
        //                Assert.Equal(4, pool.DisposedBlocks);
        //            }
        //        }

        //        [Fact]
        //        public async Task NewSegmentsAllocatedWhenBufferReachesMinimumReadSize()
        //        {
        //            // We're using the pipe here as a way to pump bytes into the reader asynchronously
        //            var pipe = new Pipe();
        //            var options = new StreamPipeReaderOptions(pool: new HeapBufferPool(), bufferSize: 10, minimumReadSize: 5);
        //            var reader = PipeReader.Create(pipe.Reader.AsStream(), options);

        //            pipe.Writer.WriteEmpty(6);
        //            await pipe.Writer.FlushAsync();

        //            var readResult = await reader.ReadAsync();
        //            Assert.Equal(6, readResult.Buffer.Length);
        //            Assert.True(readResult.Buffer.IsSingleSegment);
        //            reader.AdvanceTo(readResult.Buffer.Start, readResult.Buffer.End);

        //            pipe.Writer.WriteEmpty(4);
        //            await pipe.Writer.FlushAsync();

        //            readResult = await reader.ReadAsync();
        //            Assert.Equal(10, readResult.Buffer.Length);
        //            Assert.False(readResult.Buffer.IsSingleSegment);
        //            var segments = 0;
        //            foreach (var segment in readResult.Buffer)
        //            {
        //                segments++;
        //            }
        //            Assert.Equal(2, segments);
        //            reader.AdvanceTo(readResult.Buffer.End);

        //            reader.Complete();

        //            pipe.Writer.Complete();
        //        }
        #endregion

        [Fact]
        public void CompletingTheReadingDisposesUnderlying()
        {
            var underlying = new ObserveCompletePipeReader();
            PipeReader reader = new MessagePipeReader(underlying, new TestProtocol());
            reader.Complete();

            Assert.True(underlying.Completed);
        }

        [Fact]
        public void OnWriterCompletedNoops()
        {
            var fired = false;
            var reader = new MessagePipeReader(PipeReader.Create(Stream.Null), new TestProtocol());
#pragma warning disable CS0618 // Type or member is obsolete
            reader.OnWriterCompleted((_, __) => { fired = true; }, null);
#pragma warning restore CS0618 // Type or member is obsolete
            reader.Complete();
            Assert.False(fired);
        }

        [Fact]
        public async Task InvalidCursorThrows()
        {
            var protocol = new TestProtocol();
            var pipe = new Pipe();
            pipe.Writer.WriteEmpty(protocol, 10);
            await pipe.Writer.FlushAsync();

            var readResult = await pipe.Reader.ReadAsync();
            var buffer = readResult.Buffer;

            var reader = new MessagePipeReader(PipeReader.Create(Stream.Null), new TestProtocol());
            //QUESTION: This is not the same as StreamPipeReader which throws InvalidOperationException.
            // Is fixing this worth the added complexity?
            Assert.Throws<ArgumentOutOfRangeException>(() => reader.AdvanceTo(buffer.Start, buffer.End));

            pipe.Reader.Complete();
            pipe.Writer.Complete();

            reader.Complete();
        }

        [Fact]
        public void NullPipeReaderThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new MessagePipeReader(null, new TestProtocol()));
        }

        [Fact]
        public void NullMessageReaderThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new MessagePipeReader(PipeReader.Create(Stream.Null), null));
        }

        [Fact]
        public async Task CanReadLargeMessages()
        {
            var reader = await CreateReaderOverBytes(new byte[10000]).ConfigureAwait(false);

            var readResult = await reader.ReadAsync();
            Assert.Equal(10000, readResult.Buffer.Length);
            reader.AdvanceTo(readResult.Buffer.End);
            reader.Complete();
        }

        [Fact]
        public async Task ReadMessagesAsynchronouslyWorks()
        {
            var options = new PipeOptions(useSynchronizationContext: false, readerScheduler: PipeScheduler.Inline, writerScheduler: PipeScheduler.Inline);
            var pair = DuplexPipe.CreateConnectionPair(options, options);
            await using var connection = new DefaultConnectionContext(Guid.NewGuid().ToString(), pair.Transport, pair.Application);
            var data = Encoding.UTF8.GetBytes("Hello World");
            var protocol = new TestProtocol();

            async Task WritingTask()
            {
                var writer = connection.Application.Output;

                for (var i = 0; i < 3; i++)
                {
                    protocol.WriteMessage(data, writer);
                    await writer.FlushAsync().ConfigureAwait(false);
                }

                await writer.CompleteAsync().ConfigureAwait(false);
            }

            async Task ReadingTask()
            {
                var reader = connection.CreatePipeReader(protocol);

                while (true)
                {
                    var result = await reader.ReadAsync().ConfigureAwait(false);
                    var buffer = result.Buffer;

                    if (buffer.Length < 3 * data.Length)
                    {
                        reader.AdvanceTo(buffer.Start, buffer.End);
                        continue;
                    }

                    Assert.Equal(Enumerable.Repeat(data, 3).SelectMany(a => a).ToArray(), buffer.ToArray());

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }

                await reader.CompleteAsync();
            }

            var readingTask = ReadingTask();
            var writingTask = WritingTask();

            await writingTask;
            await readingTask;
        }

        private static async Task<string> ReadFromPipeAsString(PipeReader reader)
        {
            var readResult = await reader.ReadAsync();
            var result = Encoding.ASCII.GetString(readResult.Buffer.ToArray());
            reader.AdvanceTo(readResult.Buffer.End);
            return result;
        }

        public static IEnumerable<object[]> ReadSettings
        {
            get
            {
                yield return CreateRead(bytesInBuffer: 1024, bufferSize: 1024, minimumReadSize: 1024, readSizes: new[] { 1024, 0 });
                yield return CreateRead(bytesInBuffer: 1023, bufferSize: 512, minimumReadSize: 512, readSizes: new[] { 512, 511, 0 });
                yield return CreateRead(bytesInBuffer: 512, bufferSize: 1000, minimumReadSize: 512, readSizes: new[] { 512, 0 });
                yield return CreateRead(bytesInBuffer: 10, bufferSize: 100, minimumReadSize: 512, readSizes: new[] { 10, 0 });
                yield return CreateRead(bytesInBuffer: 8192, bufferSize: 3000, minimumReadSize: 2048, readSizes: new[] { 3000, 3000, 2192, 0 });
                yield return CreateRead(bytesInBuffer: 4096, bufferSize: 3000, minimumReadSize: 2048, readSizes: new[] { 3000, 1096, 0 });
            }
        }

        // Helper to make the above code look nicer
        private static object[] CreateRead(int bytesInBuffer, int bufferSize, int minimumReadSize, int[] readSizes)
        {
            return new object[] { bytesInBuffer, bufferSize, minimumReadSize, readSizes };
        }

        private class ThrowAfterZeroByteReadStream : MemoryStream
        {
            public ThrowAfterZeroByteReadStream()
            {

            }

            public ThrowAfterZeroByteReadStream(byte[] buffer) : base(buffer)
            {

            }

            private bool _throwOnNextCallToRead;
            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                if (_throwOnNextCallToRead)
                {
                    throw new Exception();
                }
                var bytes = await base.ReadAsync(buffer, offset, count, cancellationToken);
                if (bytes == 0)
                {
                    _throwOnNextCallToRead = true;
                }
                return bytes;
            }

#if NETCOREAPP
            public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
            {
                if (_throwOnNextCallToRead)
                {
                    throw new Exception();
                }
                var bytes = await base.ReadAsync(destination, cancellationToken);
                if (bytes == 0)
                {
                    _throwOnNextCallToRead = true;
                }
                return bytes;
            }
#endif
        }

        private class TestProtocol : IMessageReader<ReadOnlySequence<byte>>, IMessageWriter<byte[]>
        {
            public bool TryParseMessage(in ReadOnlySequence<byte> input, ref SequencePosition consumed, ref SequencePosition examined, out ReadOnlySequence<byte> message)
            {
                var reader = new SequenceReader<byte>(input);

                if (!reader.TryReadLittleEndian(out int length) || reader.Remaining < length)
                {
                    message = default;
                    return false;
                }

                message = input.Slice(reader.Position, length);
                consumed = message.End;
                examined = message.End;
                return true;
            }

            public void WriteMessage(byte[] message, IBufferWriter<byte> output)
            {
                var span = output.GetSpan(4);
                BinaryPrimitives.WriteInt32LittleEndian(span, message.Length);
                output.Advance(4);
                output.Write(message);
            }
        }
    }

    public static class TestWriterExtensions
    {
        public static PipeWriter WriteEmpty(this PipeWriter writer, IMessageWriter<byte[]> protocol, int count)
        {
            protocol.WriteMessage(new byte[count], writer);
            return writer;
        }
    }

    // This pool returns exact buffer sizes using heap memory
    public class HeapBufferPool : MemoryPool<byte>
    {
        public override int MaxBufferSize => int.MaxValue;

        public override IMemoryOwner<byte> Rent(int minBufferSize = -1)
        {
            return new Owner(minBufferSize == -1 ? 4096 : minBufferSize);
        }

        protected override void Dispose(bool disposing)
        {

        }

        private class Owner : IMemoryOwner<byte>
        {
            public Owner(int size)
            {
                Memory = new byte[size].AsMemory();
            }

            public Memory<byte> Memory { get; }

            public void Dispose()
            {

            }
        }
    }

    public abstract class ReadOnlyStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }

    public class CancelledReadsStream : ReadOnlyStream
    {
        public TaskCompletionSource<object> WaitForReadTask = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await WaitForReadTask.Task;

            cancellationToken.ThrowIfCancellationRequested();

            return 0;
        }

#if NETCOREAPP
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await WaitForReadTask.Task;

            cancellationToken.ThrowIfCancellationRequested();

            return 0;
        }
#endif
    }

    public class ObserveCompletePipeReader : PipeReader
    {
        public bool Completed { get; set; }

        public override void AdvanceTo(SequencePosition consumed)
        {
            throw new NotImplementedException();
        }

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
            throw new NotImplementedException();
        }

        public override void CancelPendingRead()
        {
            throw new NotImplementedException();
        }

        public override void Complete(Exception exception = null)
        {
            Completed = true;
        }

        public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override bool TryRead(out ReadResult result)
        {
            throw new NotImplementedException();
        }
    }
}
