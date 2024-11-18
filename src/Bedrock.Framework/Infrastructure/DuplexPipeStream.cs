﻿using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework.Infrastructure;

internal class DuplexPipeStream(PipeReader input, PipeWriter output, bool throwOnCancelled = false) : Stream
{
    private volatile bool _cancelCalled;

    public void CancelPendingRead()
    {
        _cancelCalled = true;
        input.CancelPendingRead();
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length
    {
        get
        {
            throw new NotSupportedException();
        }
    }

    public override long Position
    {
        get
        {
            throw new NotSupportedException();
        }
        set
        {
            throw new NotSupportedException();
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        // ValueTask uses .GetAwaiter().GetResult() if necessary
        // https://github.com/dotnet/corefx/blob/f9da3b4af08214764a51b2331f3595ffaf162abe/src/System.Threading.Tasks.Extensions/src/System/Threading/Tasks/ValueTask.cs#L156
        return ReadAsyncInternal(new Memory<byte>(buffer, offset, count), default).Result;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
    {
        return ReadAsyncInternal(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();
    }

    public override ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
    {
        return ReadAsyncInternal(destination, cancellationToken);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        WriteAsync(buffer, offset, count).GetAwaiter().GetResult();
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (buffer != null)
        {
            output.Write(new ReadOnlySpan<byte>(buffer, offset, count));
        }

        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
    {
        output.Write(source.Span);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public override void Flush()
    {
        FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return WriteAsync(null, 0, 0, cancellationToken);
    }

    private async ValueTask<int> ReadAsyncInternal(Memory<byte> destination, CancellationToken cancellationToken)
    {
        while (true)
        {
            var result = await input.ReadAsync(cancellationToken).ConfigureAwait(false);
            var readableBuffer = result.Buffer;
            try
            {
                if (throwOnCancelled && result.IsCanceled && _cancelCalled)
                {
                    // Reset the bool
                    _cancelCalled = false;
                    throw new OperationCanceledException();
                }

                if (!readableBuffer.IsEmpty)
                {
                    // buffer.Count is int
                    var count = (int)Math.Min(readableBuffer.Length, destination.Length);
                    readableBuffer = readableBuffer.Slice(0, count);
                    readableBuffer.CopyTo(destination.Span);
                    return count;
                }

                if (result.IsCompleted)
                {
                    return 0;
                }
            }
            finally
            {
                input.AdvanceTo(readableBuffer.End, readableBuffer.End);
            }
        }
    }

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
    {
        return TaskToApm.Begin(ReadAsync(buffer, offset, count), callback, state);
    }

    public override int EndRead(IAsyncResult asyncResult)
    {
        return TaskToApm.End<int>(asyncResult);
    }

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
    {
        return TaskToApm.Begin(WriteAsync(buffer, offset, count), callback, state);
    }

    public override void EndWrite(IAsyncResult asyncResult)
    {
        TaskToApm.End(asyncResult);
    }
}
