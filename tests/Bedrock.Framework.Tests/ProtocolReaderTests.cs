using static Bedrock.Framework.Experimental.Tests.Infrastructure.ConnectionsHelper;
using Bedrock.Framework.Experimental.Tests.Infrastructure;
using Bedrock.Framework.Protocols;
using System.Threading.Tasks;
using System.IO.Pipelines;
using static Xunit.Assert;
using System.Threading;
using System.Buffers;
using System.Text;
using System.IO;
using System;
using Xunit;

#pragma warning disable CA2012, CS8602

namespace Bedrock.Framework.Tests
{
    public class ProtocolReaderTests
    {
        [Theory, InlineData(10), InlineData(100), InlineData(1000)]
        public async Task ReadMessagesWorks(int count)
        {
            await using var connection = CreateNewConnectionContext();
            var data = Encoding.UTF8.GetBytes("Hello World");
            for (int i = 0; i < count; i++)
            {
                await connection.Application!.Output.WriteAsync(data);
            }

            await connection.Application!.Output.CompleteAsync();

            var protocol = new TestProtocol(data.Length);
            var reader = connection.CreateReader();
            var readCount = 0;

            while (true)
            {
                var result = await reader.ReadAsync(protocol);
                if (result.IsCompleted) break;

                readCount++;
                Equal(data, result.Message);

                reader.Advance();
            }

            Equal(count, readCount);
        }

        [Theory, InlineData(10), InlineData(100), InlineData(1000)]
        public async Task ReadMessagesAsynchronouslyWorks(int count)
        {
            var options = new PipeOptions(useSynchronizationContext: false, readerScheduler: PipeScheduler.Inline, writerScheduler: PipeScheduler.Inline);
            await using var connection = CreateNewConnectionContext(options);
            var data = Encoding.UTF8.GetBytes("Hello World");
            var protocol = new TestProtocol(data.Length);

            async Task WritingTask()
            {
                for (int i = 0; i < count; i++)
                {
                    await connection.Application!.Output.WriteAsync(data);
                }

                await connection.Application!.Output.CompleteAsync();
            }

            async Task ReadingTask()
            {
                await using var reader = connection.CreateReader();
                var readCount = 0;

                while (true)
                {
                    var result = await reader.ReadAsync(protocol);

                    if (result.IsCompleted)
                    {
                        break;
                    }

                    readCount++;
                    Equal(data, result.Message);

                    reader.Advance();
                }

                Equal(count, readCount);
            }

            await Task.WhenAll(ReadingTask(), WritingTask());
        }

        [Theory]
        [InlineData(null)]
        [InlineData(20)]
        public async Task PartialMessageWorks(int? maxMessageSize)
        {
            await using var connection = CreateNewConnectionContext();
            var data = Encoding.UTF8.GetBytes("Hello World");

            var protocol = new TestProtocol(data.Length);
            await using var reader = connection.CreateReader();
            var resultTask = reader.ReadAsync(protocol, maximumMessageSize: maxMessageSize);

            // Write byte by byte
            for (int i = 0; i < data.Length; i++)
            {
                await connection.Application!.Output.WriteAsync(data.AsMemory(i, 1));
            }

            var result = await resultTask;
            Equal(data, result.Message);
            reader.Advance();
        }

        [Fact]
        public async Task FullMessageThenPartialMessage()
        {
            await using var connection = CreateNewConnectionContext();
            var data = Encoding.UTF8.GetBytes("Hello World");

            var protocol = new TestProtocol(data.Length);
            await using var reader = connection.CreateReader();

            var resultTask = reader.ReadAsync(protocol);

            connection.Application!.Output.Write(data);
            connection.Application!.Output.Write(data.AsSpan(0, 5));
            await connection.Application.Output.FlushAsync();

            var result = await resultTask;
            Equal(data, result.Message);
            reader.Advance();

            resultTask = reader.ReadAsync(protocol);
            await connection.Application.Output.WriteAsync(data.AsMemory(5));
            result = await resultTask;
            Equal(data, result.Message);
        }

        [Fact]
        public async Task MessageBiggerThanMaxMessageSizeThrows()
        {
            await using var connection = CreateNewConnectionContext();
            var data = Encoding.UTF8.GetBytes("Hello World");

            var protocol = new TestProtocol(data.Length);
            await using var reader = connection.CreateReader();

            var resultTask = reader.ReadAsync(protocol, maximumMessageSize: 5);

            await connection.Application!.Output.WriteAsync(data);

            await ThrowsAsync<InvalidDataException>(async () => await resultTask);
        }

        [Fact]
        public async Task ReadingWithoutCallingAdvanceThrows()
        {
            await using var connection = CreateNewConnectionContext();
            var data = Encoding.UTF8.GetBytes("Hello World");

            var protocol = new TestProtocol(data.Length);
            await using var reader = connection.CreateReader();

            await connection.Application!.Output.WriteAsync(data);
            var result = await reader.ReadAsync(protocol);
            Equal(data, result.Message);

            await ThrowsAsync<InvalidOperationException>(
                async () => await reader.ReadAsync(protocol));
        }

        [Fact]
        public async Task ReadingAfterDisposeThrows()
        {
            await using var connection = CreateNewConnectionContext();
            var reader = connection.CreateReader();
            await reader.DisposeAsync();

            await ThrowsAsync<ObjectDisposedException>(
                async () => await reader.ReadAsync(new TestProtocol(1)));
        }

        [Fact]
        public async Task ReadingAfterCancellationWorks()
        {
            await using var connection = CreateNewConnectionContext();
            var data = Encoding.UTF8.GetBytes("Hello World");

            var protocol = new TestProtocol(data.Length);
            await using var reader = connection.CreateReader();

            var resultTask = reader.ReadAsync(protocol);

            connection.Transport.Input.CancelPendingRead();

            var result = await resultTask;
            True(result.IsCanceled);
            reader.Advance();

            await connection.Application!.Output.WriteAsync(data);
            result = await reader.ReadAsync(protocol);
            reader.Advance();

            Equal(data, result.Message);
        }

        [Fact]
        public async Task AdvanceAfterCancelledReadDoesNotLoseData()
        {
            await using var connection = CreateNewConnectionContext();
            var data = Encoding.UTF8.GetBytes("Hello World");

            var protocol = new TestProtocol(data.Length);
            await using var reader = connection.CreateReader();

            connection.Transport.Input.CancelPendingRead();
            await connection.Application!.Output.WriteAsync(data);
            var resultTask = reader.ReadAsync(protocol);

            var result = await resultTask;
            True(result.IsCanceled);
            reader.Advance();

            resultTask = reader.ReadAsync(protocol);
            result = await resultTask;
            reader.Advance();

            Equal(data, result.Message);
        }

        [Fact]
        public async Task ReadingAfterCompleteWorks()
        {
            await using var connection = CreateNewConnectionContext();
            var data = Encoding.UTF8.GetBytes("Hello World");

            var protocol = new TestProtocol(data.Length);
            await using var reader = connection.CreateReader();

            await connection.Application!.Output.WriteAsync(data);
            await connection.Application!.Output.CompleteAsync();

            var result = await reader.ReadAsync(protocol);
            False(result.IsCompleted);
            Equal(data, result.Message);
            reader.Advance();

            var resultTask = reader.ReadAsync(protocol);

            result = await resultTask;
            True(result.IsCompleted);
            reader.Advance();

            result = await reader.ReadAsync(protocol);
            True(result.IsCompleted);
            reader.Advance();
        }

        [Fact]
        public async Task ReadAfterCancellationTokenFiresWorks()
        {
            await using var connection = CreateNewConnectionContext();
            var data = Encoding.UTF8.GetBytes("Hello World");

            var protocol = new TestProtocol(data.Length);
            await using var reader = connection.CreateReader();

            await connection.Application!.Output.WriteAsync(data);

            var result = await reader.ReadAsync(protocol);
            Equal(data.Length, result.Message.Length);
            reader.Advance();

            var cts = new CancellationTokenSource();
            cts.Cancel();

            await ThrowsAsync<TaskCanceledException>(async () => await reader.ReadAsync(protocol, cts.Token));
            await connection.Application.Output.WriteAsync(data);

            result = await reader.ReadAsync(protocol);
            Equal(data.Length, result.Message.Length);
            reader.Advance();
        }
    }
}

#pragma warning restore CA2012, S5034, CS8602