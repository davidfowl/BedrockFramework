// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Bedrock.Framework.Infrastructure;

internal sealed class LoggingStream(Stream inner, ILogger logger, LoggingFormatter logFormatter = null) : Stream
{
    public override bool CanRead
    {
        get
        {
            return inner.CanRead;
        }
    }

    public override bool CanSeek
    {
        get
        {
            return inner.CanSeek;
        }
    }

    public override bool CanWrite
    {
        get
        {
            return inner.CanWrite;
        }
    }

    public override long Length
    {
        get
        {
            return inner.Length;
        }
    }

    public override long Position
    {
        get
        {
            return inner.Position;
        }

        set
        {
            inner.Position = value;
        }
    }

    public override void Flush()
    {
        inner.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return inner.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = inner.Read(buffer, offset, count);
        Log("Read", new ReadOnlySpan<byte>(buffer, offset, read));
        return read;
    }

    public override int Read(Span<byte> destination)
    {
        int read = inner.Read(destination);
        Log("Read", destination.Slice(0, read));
        return read;
    }

    public async override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int read = await inner.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        Log("ReadAsync", new ReadOnlySpan<byte>(buffer, offset, read));
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
    {
        int read = await inner.ReadAsync(destination, cancellationToken).ConfigureAwait(false);
        Log("ReadAsync", destination.Span.Slice(0, read));
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return inner.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        inner.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Log("Write", new ReadOnlySpan<byte>(buffer, offset, count));
        inner.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> source)
    {
        Log("Write", source);
        inner.Write(source);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        Log("WriteAsync", new ReadOnlySpan<byte>(buffer, offset, count));
        return inner.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
    {
        Log("WriteAsync", source.Span);
        return inner.WriteAsync(source, cancellationToken);
    }

    private void Log(string method, ReadOnlySpan<byte> buffer)
    {
        if (logFormatter != null)
        {
            logFormatter(logger, method, buffer);
            return;
        }

        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"{method}[{buffer.Length}]");
        var charBuilder = new StringBuilder();

        // Write the hex
        for (int i = 0; i < buffer.Length; i++)
        {
            builder.Append(buffer[i].ToString("X2"));
            builder.Append(" ");

            var bufferChar = (char)buffer[i];
            if (char.IsControl(bufferChar))
            {
                charBuilder.Append(".");
            }
            else
            {
                charBuilder.Append(bufferChar);
            }

            if ((i + 1) % 16 == 0)
            {
                builder.Append("  ");
                builder.Append(charBuilder.ToString());
                builder.AppendLine();
                charBuilder.Clear();
            }
            else if ((i + 1) % 8 == 0)
            {
                builder.Append(" ");
                charBuilder.Append(" ");
            }
        }

        if (charBuilder.Length > 0)
        {
            // 2 (between hex and char blocks) + num bytes left (3 per byte)
            builder.Append(string.Empty.PadRight(2 + (3 * (16 - charBuilder.Length))));
            // extra for space after 8th byte
            if (charBuilder.Length < 8)
                builder.Append(" ");
            builder.Append(charBuilder.ToString());
        }

        logger.LogDebug(builder.ToString());
    }

    // The below APM methods call the underlying Read/WriteAsync methods which will still be logged.
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
