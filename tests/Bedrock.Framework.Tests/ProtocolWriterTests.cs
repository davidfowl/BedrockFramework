using static Bedrock.Framework.Experimental.Tests.Infrastructure.ConnectionsHelper;
using Bedrock.Framework.Experimental.Tests.Infrastructure;
using Bedrock.Framework.Protocols;
using System.Threading.Tasks;
using System.IO.Pipelines;
using static Xunit.Assert;
using System.Threading;
using System.Text;
using System;
using Xunit;

namespace Bedrock.Framework.Tests
{
    public class ProtocolWriterTest
    {
        [Theory, InlineData(10), InlineData(100), InlineData(1000)]
        public async Task WriteMessagesWorks(int count)
        {
            await using var connection = CreateNewConnectionContext();
            var data = Encoding.UTF8.GetBytes("Hello world");

            var protocol = new TestProtocol(data.Length);
            await using var writer = connection.CreateWriter();

            for (int i = 0; i < count; i++)
            {
                await writer.WriteAsync(protocol, data);
            }

            Equal(count, writer.MessagesWritten);
            await connection.Transport.Output.CompleteAsync();

            var result = await connection.Application!.Input.ReadAsync();
            Equal(data.Length * count, result.Buffer.Length);
            True(result.IsCompleted);
        }

        [Theory, InlineData(10), InlineData(100), InlineData(1000)]
        public async Task WriteMessagesAsynchronouslyWorks(int count)
        {
            var options = new PipeOptions(useSynchronizationContext: false, readerScheduler: PipeScheduler.Inline, writerScheduler: PipeScheduler.Inline);
            await using var connection = CreateNewConnectionContext(options);
            var data = Encoding.UTF8.GetBytes("Hello world");
            var protocol = new TestProtocol(data.Length);

            async Task WritingTask()
            {
                await using var writer = connection.CreateWriter();

                for (int i = 0; i < count; i++)
                {
                    await writer.WriteAsync(protocol, data);
                }

                Equal(count, writer.MessagesWritten);
                await connection.Transport.Output.CompleteAsync();
            }

            async Task ReadingTask()
            {
                while (true)
                {
                    var result = await connection.Application!.Input.ReadAsync();
                    if (result.IsCompleted) break;

                    var buffer = result.Buffer;
                    SequencePosition consumed = buffer.Start, examined = buffer.Start;
                    while (protocol.TryParseMessage(in buffer, ref consumed, ref examined, out var message))
                    {
                        Equal(data, message);
                        buffer = buffer.Slice(consumed);
                    }

                    connection.Application!.Input.AdvanceTo(consumed, examined);
                }
            }

            await Task.WhenAll(ReadingTask(), WritingTask());
        }

        [Fact]
        public async Task WritingAfterDisposeThrows()
        {
            await using var connection = CreateNewConnectionContext();
            var data = Encoding.UTF8.GetBytes("Hello world");

            var protocol = new TestProtocol(data.Length);
            var writer = connection.CreateWriter();
            await writer.DisposeAsync();

            await ThrowsAsync<ObjectDisposedException>(() => writer.WriteAsync(protocol, data).AsTask());
            await ThrowsAsync<ObjectDisposedException>(() => writer.WriteManyAsync(protocol, new[] { data }).AsTask());

            Equal(0, writer.MessagesWritten);

            await connection.Transport.Output.CompleteAsync();

            var read = await connection.Application!.Input.ReadAsync();
            Equal(0, read.Buffer.Length);
            True(read.IsCompleted);
        }

        [Fact]
        public async Task WritingAfterSemaphoreDisposeThrows()
        {
            await using var connection = CreateNewConnectionContext();
            var data = Encoding.UTF8.GetBytes("Hello world");

            var protocol = new TestProtocol(data.Length);
            var singleWriter = new SemaphoreSlim(1, 1);
            var writer = connection.CreateWriter(singleWriter);
            singleWriter.Dispose();

            await ThrowsAsync<ObjectDisposedException>(() => writer.WriteAsync(protocol, data).AsTask());
            await ThrowsAsync<ObjectDisposedException>(() => writer.WriteManyAsync(protocol, new[] { data }).AsTask());

            Equal(0, writer.MessagesWritten);

            await connection.Transport.Output.CompleteAsync();

            var read = await connection.Application!.Input.ReadAsync();
            Equal(0, read.Buffer.Length);
            True(read.IsCompleted);
        }

        [Fact]
        public async Task WritingAfterCompleteThrows()
        {
            await using var connection = CreateNewConnectionContext();
            var data = Encoding.UTF8.GetBytes("Hello world");

            var protocol = new TestProtocol(data.Length);
            await using var writer = connection.CreateWriter();
            await connection.Transport.Output.CompleteAsync();

            await ThrowsAsync<ObjectDisposedException>(() => writer.WriteManyAsync(protocol, new[] { data }).AsTask());
            Equal(0, writer.MessagesWritten);
        }

        [Fact]
        public async Task WritingAfterCancellationWorks()
        {
            await using var connection = CreateNewConnectionContext();
            var data = Encoding.UTF8.GetBytes("Hello world");

            var protocol = new TestProtocol(data.Length);
            using var singleWriter = new SemaphoreSlim(1, 1);
            await using var writer = connection.CreateWriter(singleWriter);

            async Task VerifyThrowsOperationCanceled(Func<ValueTask> writeAsyncFactory)
            {
                // block the write until we cancels the pending flush
                await singleWriter.WaitAsync();
                var writeAsync = writeAsyncFactory();

                // cancel the next flush
                connection.Transport.Output.CancelPendingFlush();

                // release once we cancelled the pending flush
                singleWriter.Release();

                await ThrowsAsync<OperationCanceledException>(writeAsync.AsTask);
            }

            await VerifyThrowsOperationCanceled(() => writer.WriteAsync(protocol, data));
            await VerifyThrowsOperationCanceled(() => writer.WriteManyAsync(protocol, new[] { data }));

            await writer.WriteAsync(protocol, data);
            Equal(1, writer.MessagesWritten);
        }

        [Fact]
        public async Task WriteAfterCancellationTokenFiresWorks()
        {
            await using var connection = CreateNewConnectionContext();
            var data = Encoding.UTF8.GetBytes("Hello world");

            var protocol = new TestProtocol(data.Length);
            await using var writer = connection.CreateWriter();

            var cts = new CancellationTokenSource();
            cts.Cancel();

            await ThrowsAsync<OperationCanceledException>(() => writer.WriteAsync(protocol, data, cts.Token).AsTask());
            await ThrowsAsync<OperationCanceledException>(() => writer.WriteManyAsync(protocol, new[] { data }, cts.Token).AsTask());

            await writer.WriteAsync(protocol, data, CancellationToken.None);
            Equal(1, writer.MessagesWritten);
        }
    }
}
