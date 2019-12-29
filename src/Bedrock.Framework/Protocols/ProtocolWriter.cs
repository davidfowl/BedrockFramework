using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework.Protocols
{
    public class ProtocolWriter : IAsyncDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed;

        public ProtocolWriter(ConnectionContext connection)
            : this(connection, new SemaphoreSlim(1))
        {
        }

        public ProtocolWriter(ConnectionContext connection, SemaphoreSlim semaphore)
        {
            Connection = connection;
            _semaphore = semaphore;
        }

        public ConnectionContext Connection { get; }

        public async ValueTask WriteAsync<TWriteMessage>(IMessageWriter<TWriteMessage> writer, TWriteMessage protocolMessage, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                if (_disposed)
                {
                    return;
                }

                writer.WriteMessage(protocolMessage, Connection.Transport.Output);

                var result = await Connection.Transport.Output.FlushAsync(cancellationToken).ConfigureAwait(false);

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

                foreach (var protocolMessage in protocolMessages)
                {
                    writer.WriteMessage(protocolMessage, Connection.Transport.Output);
                }

                var result = await Connection.Transport.Output.FlushAsync(cancellationToken).ConfigureAwait(false);

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
