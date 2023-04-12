using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO.Pipelines;
using System.Threading;
using System.IO;
using System;

namespace Bedrock.Framework.Protocols
{
    // REVIEW: Made most members protected internal if we need to add an extension method to write a raw ReadOnlyMemory<T>
    //         to the pipe writer using the synchronization mechanism provided by this class ?
    //         (which should be an extension method I assume since the scope of this class is to write messages)
    public class ProtocolWriter : IAsyncDisposable
    {
        protected internal bool _disposed, _singleWriterDisposed, _shouldDisposeSingleWriter;
        protected internal SemaphoreSlim _singleWriter;
        protected internal PipeWriter _writer;
        private long _messagesWritten;

        // REVIEW: should we sync this over the semaphore ?
        public long MessagesWritten => Interlocked.Read(ref _messagesWritten);

        public ProtocolWriter(PipeWriter writer, SemaphoreSlim singleWriter)
            => (_writer, _singleWriter) = (writer, singleWriter);

        public ProtocolWriter(Stream writer, SemaphoreSlim singleWriter)
            : this(PipeWriter.Create(writer), singleWriter)
        {
        }

        public ProtocolWriter(PipeWriter writer) : this(writer, new SemaphoreSlim(1, 1))
            => _shouldDisposeSingleWriter = true;

        public ProtocolWriter(Stream writer) : this(PipeWriter.Create(writer))
        {
        }

        public ValueTask WriteAsync<TWriteMessage>(IMessageWriter<TWriteMessage> writer,
            TWriteMessage message, CancellationToken cancellationToken = default)
        {
            // Try to grab the lock synchronously first, if we can't then go async
#pragma warning disable CA2016 // This will always finish synchronously so we do not need to bother with cancel
            if (!TryWaitForSingleWriter(0)) return WriteAsyncSlow(writer, message, cancellationToken);
#pragma warning restore CA2016

            // no need to enter the try/finally if the semaphore has been disposed
            if (_singleWriterDisposed)
                return _disposed // dispose this instance if it hasn't been disposed yet
                    ? default
                    : DisposeAsync();

            bool release = true, hasWritten = false, dispose = false;
            try
            {
                if (_disposed) return default;

                var task = WriteCore(writer, in message, cancellationToken);

                if (!task.IsCompletedSuccessfully)
                {
                    release = false; // do not release if we need to go async to complete the write
                    return new ValueTask(CompleteWriteAsync(task, 1));
                }
                else
                {
                    // If it's a IValueTaskSource backed ValueTask,
                    // inform it its result has been read so it can reset
                    var result = task.GetAwaiter().GetResult();

                    if (result.IsCanceled)
                        throw new OperationCanceledException();

                    if (!result.IsCompleted)
                        hasWritten = true;
                    else
                        dispose = true;
                }
            }
            finally { if (release) ReleaseSingleWriter(hasWritten ? 1 : 0); }

            return dispose ? DisposeAsync() : default;
        }

        private async ValueTask WriteAsyncSlow<TWriteMessage>(IMessageWriter<TWriteMessage> writer,
            TWriteMessage message, CancellationToken cancellationToken = default)
        {
            await _singleWriter.WaitAsync(cancellationToken).ConfigureAwait(false);

            bool hasWritten = false, dispose = false;
            try
            {
                if (_disposed) return;

                var result = await WriteCore(writer, in message, cancellationToken).ConfigureAwait(false);

                if (result.IsCanceled)
                    throw new OperationCanceledException();

                if (!result.IsCompleted)
                    hasWritten = true;
                else
                    dispose = true;
            }
            finally { ReleaseSingleWriter(hasWritten ? 1 : 0); }

            if (dispose) await DisposeAsync().ConfigureAwait(false);
        }

        public ValueTask WriteManyAsync<TWriteMessage>(IMessageWriter<TWriteMessage> writer,
            TWriteMessage[] messages, CancellationToken cancellationToken = default)
        {
            // Try to grab the lock synchronously first, if we can't then go async
#pragma warning disable CA2016 // This will always finish synchronously so we do not need to bother with cancel
            if (!TryWaitForSingleWriter(0)) return WriteManyAsyncSlow(writer, messages, cancellationToken);
#pragma warning restore CA2016

            // no need to enter the try/finally if the semaphore has been disposed
            if (_singleWriterDisposed)
                return _disposed // dispose this instance if it hasn't been disposed yet
                    ? default
                    : DisposeAsync();

            bool release = true, hasWritten = false, dispose = false;
            try
            {
                if (_disposed) return default;

                var task = WriteManyCore(writer, messages, cancellationToken);

                if (!task.IsCompletedSuccessfully)
                {
                    release = false; // do not release if we need to go async to complete the write
                    return new ValueTask(CompleteWriteAsync(task, 1));
                }
                else
                {
                    // If it's a IValueTaskSource backed ValueTask,
                    // inform it its result has been read so it can reset
                    var result = task.GetAwaiter().GetResult();

                    if (result.IsCanceled)
                        throw new OperationCanceledException();

                    if (!result.IsCompleted)
                        hasWritten = true;
                    else
                        dispose = true;
                }
            }
            finally { if (release) ReleaseSingleWriter(hasWritten ? 1 : 0); }

            return dispose ? DisposeAsync() : default;
        }

        private async ValueTask WriteManyAsyncSlow<TWriteMessage>(IMessageWriter<TWriteMessage> writer,
            TWriteMessage[] messages, CancellationToken cancellationToken = default)
        {
            await _singleWriter.WaitAsync(cancellationToken).ConfigureAwait(false);

            bool hasWritten = false, dispose = false;
            try
            {
                if (_disposed) return;

                var result = await WriteManyCore(writer, messages, cancellationToken).ConfigureAwait(false);

                if (result.IsCanceled)
                    throw new OperationCanceledException();

                if (!result.IsCompleted)
                    hasWritten = true;
                else
                    dispose = true;
            }
            finally { ReleaseSingleWriter(hasWritten ? 1 : 0); }

            if (dispose) await DisposeAsync().ConfigureAwait(false);
        }

        public ValueTask WriteManyAsync<TWriteMessage>(IMessageWriter<TWriteMessage> writer,
            IEnumerable<TWriteMessage> messages, CancellationToken cancellationToken = default)
        {
            // Try to grab the lock synchronously first, if we can't then go async
#pragma warning disable CA2016 // This will always finish synchronously so we do not need to bother with cancel
            if (!TryWaitForSingleWriter(0)) return WriteManyAsyncSlow(writer, messages, cancellationToken);
#pragma warning restore CA2016

            // no need to enter the try/finally if the semaphore has been disposed
            if (_singleWriterDisposed)
                return _disposed // dispose this instance if it hasn't been disposed yet
                    ? default
                    : DisposeAsync();

            bool release = true, hasWritten = false, dispose = false;
            try
            {
                if (_disposed) return default;

                var task = WriteManyCore(writer, messages, cancellationToken);

                if (!task.IsCompletedSuccessfully)
                {
                    release = false; // do not release if we need to go async to complete the write
                    return new ValueTask(CompleteWriteAsync(task, 1));
                }
                else
                {
                    // If it's a IValueTaskSource backed ValueTask,
                    // inform it its result has been read so it can reset
                    var result = task.GetAwaiter().GetResult();

                    if (result.IsCanceled)
                        throw new OperationCanceledException();

                    if (!result.IsCompleted)
                        hasWritten = true;
                    else
                        dispose = true;
                }
            }
            finally { if (release) ReleaseSingleWriter(hasWritten ? 1 : 0); }

            return dispose ? DisposeAsync() : default;
        }

        private async ValueTask WriteManyAsyncSlow<TWriteMessage>(IMessageWriter<TWriteMessage> writer,
            IEnumerable<TWriteMessage> messages, CancellationToken cancellationToken = default)
        {
            await _singleWriter.WaitAsync(cancellationToken).ConfigureAwait(false);

            bool hasWritten = false, dispose = false;
            try
            {
                if (_disposed) return;

                var result = await WriteManyCore(writer, messages, cancellationToken).ConfigureAwait(false);

                if (result.IsCanceled)
                    throw new OperationCanceledException();

                if (!result.IsCompleted)
                    hasWritten = true;
                else
                    dispose = true;
            }
            finally { ReleaseSingleWriter(hasWritten ? 1 : 0); }

            if (dispose) await DisposeAsync().ConfigureAwait(false);
        }

        private const string NoWritingAllowed = "System.IO.Pipelines.ThrowHelper.ThrowInvalidOperationException_NoWritingAllowed()";

        private ValueTask<FlushResult> WriteCore<TWriteMessage>(IMessageWriter<TWriteMessage> writer,
            in TWriteMessage message, CancellationToken cancellationToken)
        {
            try
            {
                writer.WriteMessage(message, _writer); // this could throw if the pipe was completed
                return _writer.FlushAsync(cancellationToken);
            }
            catch (InvalidOperationException _) when (_.StackTrace?.Contains(NoWritingAllowed) ?? false)
            {
                return new ValueTask<FlushResult>(new FlushResult(cancellationToken.IsCancellationRequested, isCompleted: true));
            }
        }

        private ValueTask<FlushResult> WriteManyCore<TWriteMessage>(IMessageWriter<TWriteMessage> writer,
            TWriteMessage[] messages, CancellationToken cancellationToken)
        {
            try
            {
                for (int i = 0; i < messages.Length; i++)
                    writer.WriteMessage(messages[i], _writer);

                return _writer.FlushAsync(cancellationToken);
            }
            catch (InvalidOperationException _) when (_.StackTrace?.Contains(NoWritingAllowed) ?? false)
            {
                return new ValueTask<FlushResult>(new FlushResult(cancellationToken.IsCancellationRequested, isCompleted: true));
            }
        }

        private ValueTask<FlushResult> WriteManyCore<TWriteMessage>(IMessageWriter<TWriteMessage> writer,
            IEnumerable<TWriteMessage> messages, CancellationToken cancellationToken)
        {
            try
            {
                foreach (var message in messages)
                    writer.WriteMessage(message, _writer);

                return _writer.FlushAsync(cancellationToken);
            }
            catch (InvalidOperationException _) when (_.StackTrace?.Contains(NoWritingAllowed) ?? false)
            {
                return new ValueTask<FlushResult>(new FlushResult(cancellationToken.IsCancellationRequested, isCompleted: true));
            }
        }

        protected internal async Task CompleteWriteAsync(ValueTask<FlushResult> flushAsync, int messagesWritten)
        {
            bool hasWritten = false, dispose = false;
            try
            {
                var result = await flushAsync.ConfigureAwait(false);

                if (result.IsCanceled)
                    throw new OperationCanceledException();

                if (!result.IsCompleted)
                    hasWritten = true;
                else
                    dispose = true;
            }
            finally { ReleaseSingleWriter(hasWritten ? messagesWritten : 0); }

            if (dispose) await DisposeAsync().ConfigureAwait(false);
        }

        protected internal bool TryWaitForSingleWriter(int timeout, CancellationToken cancellationToken = default)
        {
            try { return _singleWriter.Wait(timeout, cancellationToken); }
            catch (ObjectDisposedException) { // swallow and no-op if the semaphore has already been disposed
                if (_shouldDisposeSingleWriter) _shouldDisposeSingleWriter = false;
                if (!_singleWriterDisposed) _singleWriterDisposed = true;

                return true; // consider the lock acquired, the dispose state should then be checked
            }
        }

        protected internal int ReleaseSingleWriter(int messagesWritten)
        {
            if (messagesWritten > 0)
                _messagesWritten += messagesWritten;

            try { return _singleWriter.Release(); }
            catch (ObjectDisposedException) { // swallow and no-op if the semaphore has already been disposed
                if (_shouldDisposeSingleWriter) _shouldDisposeSingleWriter = false;
                if (!_singleWriterDisposed) _singleWriterDisposed = true;

                return 0;
            }
        }

        public async ValueTask DisposeAsync()
        {
            // Perform async cleanup.
            await DisposeAsyncCore().ConfigureAwait(false);

            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        protected virtual ValueTask DisposeAsyncCore()
        {
            if (_singleWriterDisposed && _disposed) return default;
            if (!TryWaitForSingleWriter(0)) return disposeAsyncSlow();

            DisposeCore();
            return default;

            void DisposeCore()
            {
                try
                {
                    if (_disposed) return;

                    _disposed = true;
                }
                finally
                {
                    if (_shouldDisposeSingleWriter)
                    {
                        try { _singleWriter.Dispose(); } catch { /* discard any exception here */ }
                        _shouldDisposeSingleWriter = false;
                        _singleWriterDisposed = true;
                    }
                    else ReleaseSingleWriter(0);
                }
            }
            async ValueTask disposeAsyncSlow()
            {
                await _singleWriter.WaitAsync().ConfigureAwait(false);

                DisposeCore();
            }
        }
    }
}
