using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework.Protocols
{
    // REVIEW : What should we do with the cancelled messages ?
    //          Should we handle the semaphore lifetime and dispose it if this class owns it ?
    public class ProtocolWriter : IAsyncDisposable, IDisposable
    {
        protected internal SemaphoreSlim _singleWriter;
        protected internal PipeWriter _writer;

        private long _messagesWritten;
        private bool _disposed;

        // REVIEW: should we sync this over the semaphore ?
        public long MessagesWritten => Interlocked.Read(ref _messagesWritten);

        public ProtocolWriter(PipeWriter writer, SemaphoreSlim singleWriter)
            => (_writer, _singleWriter) = (writer, singleWriter);

        public ProtocolWriter(Stream writer, SemaphoreSlim singleWriter)
            : this(PipeWriter.Create(writer), singleWriter)
        {
        }

        public ProtocolWriter(PipeWriter writer)
            : this(writer, new SemaphoreSlim(1, 1))
        {
        }

        public ProtocolWriter(Stream writer)
            : this(writer, new SemaphoreSlim(1, 1))
        {
        }

        public ValueTask WriteAsync<TWriteMessage>(IMessageWriter<TWriteMessage> writer,
        TWriteMessage message, CancellationToken cancellationToken = default)
        {
            if (!TryWaitForSingleWriter(0, cancellationToken))
                return WriteAsyncSlow(writer, message, cancellationToken);

            bool release = true, hasWritten = true;
            try {
                if (_disposed) {
                    hasWritten = false;
                    return default;
                }

                writer.WriteMessage(message, _writer);
                var flushAsync = _writer.FlushAsync(cancellationToken);
                release = flushAsync.IsCompleted;

                if (!release) return awaitFlushAndRelease(flushAsync);

                var result = flushAsync.Result;

                if (result.IsCanceled || result.IsCompleted)
                    hasWritten = false;

                if (result.IsCompleted) _disposed = true;

                if (result.IsCanceled)
                    throw new OperationCanceledException();
                
                return default;
            }
            // the pipe was completed while writing a message, rethrow
            catch (InvalidOperationException) { hasWritten = false; throw; }
            finally { if (release) Release(hasWritten ? 1 : 0); }
            async ValueTask awaitFlushAndRelease(ValueTask<FlushResult> flushAsync)
            {
                bool written = true;
                try {
                    var result = await flushAsync.ConfigureAwait(false);
                    if (result.IsCanceled || result.IsCompleted)
                        written = false;

                    if (result.IsCompleted)
                        _disposed = true;

                    if (result.IsCanceled)
                        throw new OperationCanceledException();
                }
                // the pipe was completed while writing a message, rethrow
                catch (InvalidOperationException) { written = false; throw; }
                finally { Release(written ? 1 : 0); }
            }
        }

        async ValueTask WriteAsyncSlow<TWriteMessage>(IMessageWriter<TWriteMessage> writer,
            TWriteMessage message, CancellationToken cancellationToken = default)
        {
            await _singleWriter.WaitAsync(cancellationToken).ConfigureAwait(false);

            bool hasWritten = true;
            try {
                if (_disposed) {
                    hasWritten = false;
                    return;
                }

                writer.WriteMessage(message, _writer);

                // REVIEW: is this fast path needed since we already paid the cost of async once ?
                var flushAsync = _writer.FlushAsync(cancellationToken);
                var result = !flushAsync.IsCompletedSuccessfully
                    ? await flushAsync.ConfigureAwait(false)
                    : flushAsync.Result;

                if (result.IsCanceled || result.IsCompleted)
                    hasWritten = false;

                if (result.IsCompleted) _disposed = true;

                if (result.IsCanceled)
                    throw new OperationCanceledException();
            }
            // the pipe was completed while writing a message, rethrow
            catch (InvalidOperationException) { hasWritten = false; throw; }
            finally { Release(hasWritten ? 1 : 0); }
        }

        public ValueTask WriteManyAsync<TWriteMessage>(IMessageWriter<TWriteMessage> writer,
        TWriteMessage[] messages, CancellationToken cancellationToken = default)
        {
            if (!TryWaitForSingleWriter(0, cancellationToken))
                return WriteManyAsyncSlow(writer, messages, cancellationToken);

            bool release = true, hasWritten = true;
            try {
                if (_disposed) {
                    hasWritten = false;
                    return default;
                }

                for (int i = 0; i < messages.Length; i++)
                    writer.WriteMessage(messages[i], _writer);

                var flushAsync = _writer.FlushAsync(cancellationToken);
                release = flushAsync.IsCompletedSuccessfully;

                if (!release) return awaitFlushAndRelease(flushAsync, messages.Length);

                var result = flushAsync.Result;

                if (result.IsCanceled || result.IsCompleted)
                    hasWritten = false;

                if (result.IsCompleted) _disposed = true;

                if (result.IsCanceled)
                    throw new OperationCanceledException();
                
                return default;
            }
            // the pipe was completed while writing a message, rethrow
            catch (InvalidOperationException) { hasWritten = false; throw; }
            finally { if (release) Release(hasWritten ? messages.Length : 0); }
            

            async ValueTask awaitFlushAndRelease(ValueTask<FlushResult> flushAsync, int messagesWritten)
            {
                bool written = true;
                try {
                    var result = await flushAsync.ConfigureAwait(false);
                    if (result.IsCanceled || result.IsCompleted)
                        written = false;

                    if (result.IsCompleted)
                        _disposed = true;

                    if (result.IsCanceled)
                        throw new OperationCanceledException();
                }
                // the pipe was completed while writing a message, rethrow
                catch (InvalidOperationException) { written = false; throw; }
                finally { Release(written ? messagesWritten : 0); }
            }
        }

        async ValueTask WriteManyAsyncSlow<TWriteMessage>(IMessageWriter<TWriteMessage> writer,
            TWriteMessage[] messages, CancellationToken cancellationToken = default)
        {
            await _singleWriter.WaitAsync(cancellationToken).ConfigureAwait(false);

            bool hasWritten = true;
            try {
                if (_disposed) {
                    hasWritten = false;
                    return;
                }

                for (int i = 0; i < messages.Length; i++)
                    writer.WriteMessage(messages[i], _writer);

                // REVIEW: is this fast path needed since we already paid the cost of async once ?
                var flushAsync = _writer.FlushAsync(cancellationToken);
                var result = !flushAsync.IsCompletedSuccessfully
                    ? await flushAsync.ConfigureAwait(false)
                    : flushAsync.Result;

                if (result.IsCanceled || result.IsCompleted)
                    hasWritten = false;

                if (result.IsCompleted) _disposed = true;

                if (result.IsCanceled)
                    throw new OperationCanceledException();
            }
            // the pipe was completed while writing a message, rethrow
            catch (InvalidOperationException) { hasWritten = false; throw; }
            finally { Release(hasWritten ? messages.Length : 0); }
        }

        public ValueTask WriteManyAsync<TWriteMessage>(IMessageWriter<TWriteMessage> writer,
            IEnumerable<TWriteMessage> messages, CancellationToken cancellationToken = default)
        {
            if (!TryWaitForSingleWriter(0, cancellationToken))
                return WriteManyAsyncSlow(writer, messages, cancellationToken);

            int messagesWritten = 0;
            bool release = true, hasWritten = true;
            try {
                if (_disposed) {
                    hasWritten = false;
                    return default;
                }

                foreach (var message in messages) {
                    writer.WriteMessage(message, _writer);
                    messagesWritten++;
                }

                var flushAsync = _writer.FlushAsync(cancellationToken);
                release = flushAsync.IsCompletedSuccessfully;

                if (!release) return awaitFlushAndRelease(flushAsync, messagesWritten);

                var result = flushAsync.Result;

                if (result.IsCanceled || result.IsCompleted)
                    hasWritten = false;

                if (result.IsCompleted) _disposed = true;

                if (result.IsCanceled)
                    throw new OperationCanceledException();
                
                return default;
            }
            // the pipe was completed while writing a message, rethrow
            catch (InvalidOperationException) { hasWritten = false; throw; }
            finally { if (release) Release(hasWritten ? messagesWritten : 0); }
            async ValueTask awaitFlushAndRelease(ValueTask<FlushResult> flushAsync, int msgWritten)
            {
                bool written = true;
                try
                {
                    var result = await flushAsync.ConfigureAwait(false);
                    if (result.IsCanceled || result.IsCompleted)
                        written = false;

                    if (result.IsCompleted)
                        _disposed = true;

                    if (result.IsCanceled)
                        throw new OperationCanceledException();
                }
                // the pipe was completed while writing a message, rethrow
                catch (InvalidOperationException) { written = false; throw; }
                finally { Release(written ? msgWritten : 0); }
            }
        }

        async ValueTask WriteManyAsyncSlow<TWriteMessage>(IMessageWriter<TWriteMessage> writer,
            IEnumerable<TWriteMessage> messages, CancellationToken cancellationToken = default)
        {
            await _singleWriter.WaitAsync(cancellationToken).ConfigureAwait(false);

            bool hasWritten = true;
            int messagesWritten = 0;
            try {
                if (_disposed) {
                    hasWritten = false;
                    return;
                }

                foreach (var message in messages) {
                    writer.WriteMessage(message, _writer);
                    messagesWritten++;
                }

                // REVIEW: is this fast path needed since we already paid the cost of async once ?
                var flushAsync = _writer.FlushAsync(cancellationToken);
                var result = !flushAsync.IsCompletedSuccessfully
                    ? await flushAsync.ConfigureAwait(false)
                    : flushAsync.Result;

                if (result.IsCanceled || result.IsCompleted)
                    hasWritten = false;

                if (result.IsCompleted) _disposed = true;

                if (result.IsCanceled)
                    throw new OperationCanceledException();
            }
            // the pipe was completed while writing a message, rethrow
            catch (InvalidOperationException) { hasWritten = false; throw; }
            finally { Release(hasWritten ? messagesWritten : 0); }
        }

        protected internal bool TryWaitForSingleWriter(int timeout = 0, CancellationToken cancellationToken = default)
        {
            try { return _singleWriter.Wait(timeout, cancellationToken); }
            // no-op and dispose if the semaphore has already been disposed
            catch (ObjectDisposedException)
            {
                if (_disposed) return true;

                // the semaphore has been disposed by its owner, dispose this instance as well
                _disposed = true;
                return true;
            }
        }

        protected internal void Release(int messagesWritten)
        {
            if (messagesWritten > 0) _messagesWritten += messagesWritten;
            
            try { _singleWriter.Release(); }
            catch (ObjectDisposedException) { /* no-op and dispose if the semaphore has already been disposed */}
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            // will return true and set dispose to true if the semaphore was disposed hence no-oping
            TryWaitForSingleWriter(-1);
            if (_disposed) return;

            _disposed = true;
            Release(0);
        }

        public async ValueTask DisposeAsync()
        {
            // Perform async cleanup.
            await DisposeAsyncCore().ConfigureAwait(false);

            // Dispose of unmanaged resources.
            Dispose(false);

            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        protected virtual ValueTask DisposeAsyncCore()
        {
            // will return true and set dispose to true if the semaphore was disposed hence no-oping
            if (!TryWaitForSingleWriter()) return disposeAsyncSlow();
            if (_disposed) return default;

            _disposed = true;
            Release(0);
            return default;

            async ValueTask disposeAsyncSlow()
            {
                await _singleWriter.WaitAsync(); // wait for pending write to complete
                try
                {
                    if (_disposed) return;

                    _disposed = true;
                }
                finally { _singleWriter.Release(); }
            }
        }
    }
}
