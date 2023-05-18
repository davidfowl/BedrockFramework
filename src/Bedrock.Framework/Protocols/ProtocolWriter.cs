using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO.Pipelines;
using System.Threading;
using System.IO;
using System;

namespace Bedrock.Framework.Protocols
{
    public class ProtocolWriter : IAsyncDisposable
    {
        private readonly SemaphoreSlim _singleWriter;
        private readonly PipeWriter _pipeWriter;
        private readonly bool _dispose;
        private long _messagesWritten;
        private bool _disposed;

        public long MessagesWritten => Interlocked.Read(ref _messagesWritten);

        public ProtocolWriter(PipeWriter pipeWriter, SemaphoreSlim singleWriter)
            => (_pipeWriter, _singleWriter) = (pipeWriter, singleWriter);

        public ProtocolWriter(Stream pipeWriter, SemaphoreSlim singleWriter)
            : this(PipeWriter.Create(pipeWriter), singleWriter)
        {
        }

        public ProtocolWriter(PipeWriter pipeWriter) : this(pipeWriter, new SemaphoreSlim(1, 1)) => _dispose = true;

        public ProtocolWriter(Stream stream) : this(PipeWriter.Create(stream))
        {
        }

        public ValueTask WriteAsync<TMessage>(IMessageWriter<TMessage> writer, TMessage message, CancellationToken cancellationToken = default)
        {
            // This will always finish synchronously so we do not need to bother with cancel
            if (!TryWaitForSingleWriter(cancellationToken: CancellationToken.None))
                return WriteAsyncSlow(writer, message, cancellationToken);

            bool release = true, hasWritten = false;
            try
            {
                if (_disposed) throw new ObjectDisposedException(nameof(ProtocolWriter));

                var task = WriteCore(writer, message, cancellationToken);
                if (task.IsCompletedSuccessfully)
                {
                    // If it's a IValueTaskSource backed ValueTask,
                    // inform it its result has been read so it can reset
                    var result = task.GetAwaiter().GetResult();

                    if (result.IsCanceled)
                        throw new OperationCanceledException(cancellationToken);

                    hasWritten = !result.IsCompleted;

                    return hasWritten 
                        ? default(ValueTask)
                        // REVIEW : could DisposeAsyncCore(false) here if we add a !_dispose check between disposing && !TryWaitForSingleWriter()
                        //          but it'd require to implement a ThrowAfter extension method for ValueTask
                        : throw new ObjectDisposedException(nameof(ProtocolWriter));
                }
                else
                {
                    release = false; // do not release if we need to go async to complete the write
                    return new ValueTask(CompleteWriteAsync(task, messagesWritten: 1));
                }
            }
            finally { if (release) ReleaseSingleWriter(hasWritten ? 1 : 0); }
        }

        private async ValueTask WriteAsyncSlow<TMessage>(IMessageWriter<TMessage> writer, TMessage message, CancellationToken cancellationToken = default)
        {
            await _singleWriter.WaitAsync(cancellationToken).ConfigureAwait(false);

            bool hasWritten = false;
            try
            {
                if (_disposed) throw new ObjectDisposedException(nameof(ProtocolWriter));

                // REVIEW: is this fast path needed since we already paid the cost of async ?
                var task = WriteCore(writer, message, cancellationToken);
                var result = task.IsCompletedSuccessfully
                    ? task.GetAwaiter().GetResult()
                    : await task.ConfigureAwait(false);

                if (result.IsCanceled)
                    throw new OperationCanceledException(cancellationToken);

                hasWritten = !result.IsCompleted;
                if (!hasWritten) throw new ObjectDisposedException(nameof(ProtocolWriter));
            }
            finally { ReleaseSingleWriter(hasWritten ? 1 : 0); }
        }

        public ValueTask WriteManyAsync<TMessage>(IMessageWriter<TMessage> writer, IEnumerable<TMessage> messages,
        CancellationToken cancellationToken = default)
        {
            // This will always finish synchronously so we do not need to bother with cancel
            if (!TryWaitForSingleWriter(cancellationToken: CancellationToken.None))
                return WriteManyAsyncSlow(writer, messages, cancellationToken);

            bool release = true, hasWritten = false;
            try
            {
                if (_disposed) throw new ObjectDisposedException(nameof(ProtocolWriter));

                var task = WriteManyCore(writer, messages, cancellationToken);
                if (task.IsCompletedSuccessfully)
                {
                    // If it's a IValueTaskSource backed ValueTask,
                    // inform it its result has been read so it can reset
                    var result = task.GetAwaiter().GetResult();

                    if (result.IsCanceled)
                        throw new OperationCanceledException(cancellationToken);

                    hasWritten = !result.IsCompleted;

                    return hasWritten
                        ? default(ValueTask)
                        // REVIEW : could DisposeAsyncCore(false) here if we add a !_dispose check between disposing && !TryWaitForSingleWriter()
                        //          but it'd require to implement a ThrowAfter extension method for ValueTask
                        : throw new ObjectDisposedException(nameof(ProtocolWriter));
                }
                else
                {
                    release = false; // do not release if we need to go async to complete the write
                    return new ValueTask(CompleteWriteAsync(task, messagesWritten: 1));
                }
            }
            finally { if (release) ReleaseSingleWriter(hasWritten ? 1 : 0); }
        }

        private async ValueTask WriteManyAsyncSlow<TMessage>(IMessageWriter<TMessage> writer, IEnumerable<TMessage> messages,
            CancellationToken cancellationToken = default)
        {
            await _singleWriter.WaitAsync(cancellationToken).ConfigureAwait(false);

            bool hasWritten = false;
            try
            {
                if (_disposed) throw new ObjectDisposedException(nameof(ProtocolWriter));

                // REVIEW: is this fast path needed since we already paid the cost of async ?
                var task = WriteManyCore(writer, messages, cancellationToken);
                var result = task.IsCompletedSuccessfully
                    ? task.GetAwaiter().GetResult()
                    : await task.ConfigureAwait(false);

                if (result.IsCanceled)
                    throw new OperationCanceledException(cancellationToken);

                hasWritten = !result.IsCompleted;
                if (!hasWritten) throw new ObjectDisposedException(nameof(ProtocolWriter));
            }
            finally { ReleaseSingleWriter(hasWritten ? 1 : 0); }
        }

        private static bool IsPipeInvalidOperationException(Exception e)
            => e is { Source: "System.IO.Pipelines", Message: "Writing is not allowed after writer was completed." };

        private ValueTask<FlushResult> WriteCore<TMessage>(IMessageWriter<TMessage> writer, TMessage message, CancellationToken cancellationToken)
        {
            try
            {
                // this will throw if the pipe was completed
                writer.WriteMessage(message, _pipeWriter);
                return _pipeWriter.FlushAsync(cancellationToken);
            }
            catch (InvalidOperationException e) when (IsPipeInvalidOperationException(e))
            {
                return new ValueTask<FlushResult>(new FlushResult(cancellationToken.IsCancellationRequested, isCompleted: true));
            }
        }

        private ValueTask<FlushResult> WriteManyCore<TMessage>(IMessageWriter<TMessage> writer, IEnumerable<TMessage> messages,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (messages is TMessage[] array)
                {
                    foreach (ref readonly var message in array.AsSpan())
                        writer.WriteMessage(message, _pipeWriter);
                }
                else
                {
                    foreach (var message in messages)
                        writer.WriteMessage(message, _pipeWriter);
                }

                return _pipeWriter.FlushAsync(cancellationToken);
            }
            catch (InvalidOperationException e) when (IsPipeInvalidOperationException(e))
            {
                return new ValueTask<FlushResult>(new FlushResult(cancellationToken.IsCancellationRequested, isCompleted: true));
            }
        }

        private async Task CompleteWriteAsync(ValueTask<FlushResult> flushAsync, int messagesWritten)
        {
            bool hasWritten = false;
            try
            {
                var result = await flushAsync.ConfigureAwait(false);

                if (result.IsCanceled)
                    throw new OperationCanceledException();

                hasWritten = !result.IsCompleted;
                if (!hasWritten) throw new ObjectDisposedException(nameof(ProtocolWriter));
            }
            finally { ReleaseSingleWriter(hasWritten ? messagesWritten : 0); }
        }

        private bool TryWaitForSingleWriter(int timeout = 0, CancellationToken cancellationToken = default)
        {
            try { return _singleWriter.Wait(timeout, cancellationToken); }
            catch (ObjectDisposedException e) { throw new ObjectDisposedException(nameof(ProtocolWriter), e); }
        }

        private bool ReleaseSingleWriter(int messagesWritten)
        {
            try
            {
                _messagesWritten += messagesWritten;
                return _singleWriter.Release() == 1; // REVIEW: should we throw if the release count doesn't match ?
            }
            catch (ObjectDisposedException e) { throw new ObjectDisposedException(nameof(ProtocolWriter), e); }
        }

        public async ValueTask DisposeAsync()
        {
            // Perform async cleanup.
            await DisposeAsyncCore(true).ConfigureAwait(false);

            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        private ValueTask DisposeAsyncCore(bool disposing)
        {
            if (disposing && !TryWaitForSingleWriter())
                return DisposeAsyncSlow();

            DisposeCore(disposing);
            return default;

            void DisposeCore(bool release)
            {
                try
                {
                    if (_disposed) return;

                    _disposed = true;
                    if (_dispose) _singleWriter.Dispose();
                }
                finally { if (!_dispose && release) ReleaseSingleWriter(0); }
            }
            async ValueTask DisposeAsyncSlow()
            {
                await _singleWriter.WaitAsync().ConfigureAwait(false);

                DisposeCore(release: true);
            }
        }
    }
}
