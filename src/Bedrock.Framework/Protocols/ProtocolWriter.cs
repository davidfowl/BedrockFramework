using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework.Protocols
{
    public class ProtocolWriter : IAsyncDisposable
    {
        private readonly PipeWriter _writer;
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed;

        public ProtocolWriter(Stream stream) : 
            this(PipeWriter.Create(stream))
        {

        }

        public ProtocolWriter(PipeWriter writer)
            : this(writer, new SemaphoreSlim(1))
        {
        }

        public ProtocolWriter(PipeWriter writer, SemaphoreSlim semaphore)
        {
            _writer = writer;
            _semaphore = semaphore;
        }

        public async ValueTask WriteAsync<TWriteMessage>(IMessageWriter<TWriteMessage> writer, TWriteMessage protocolMessage, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                if (_disposed)
                {
                    return;
                }

                writer.WriteMessage(protocolMessage, _writer);

                var result = await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);

                if (result.IsCanceled)
                {
                    throw new OperationCanceledException();
                }

                if (result.IsCompleted)
                {
                    _disposed = true;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async ValueTask WriteManyAsync<TWriteMessage>(IMessageWriter<TWriteMessage> writer, IEnumerable<TWriteMessage> protocolMessages, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                if (_disposed)
                {
                    return;
                }

                foreach(var protocolMessage in protocolMessages)
                {
                    writer.WriteMessage(protocolMessage, _writer);
                }

                var result = await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);

                if (result.IsCanceled)
                {
                    throw new OperationCanceledException();
                }

                if (result.IsCompleted)
                {
                    _disposed = true;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void CancelPendingFlush()
        {
            if (_disposed)
            {
                return;
            }

            _writer.CancelPendingFlush();
        }

        public async ValueTask CompleteAsync()
        {
            if (_disposed)
            {
                return;
            }

            await _writer.CompleteAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
