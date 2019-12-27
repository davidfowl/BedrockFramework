using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework.Protocols
{
    public class ProtocolWriter<TWriteMessage> : IAsyncDisposable
    {
        private readonly IProtocolWriter<TWriteMessage> _writer;
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed;

        public ProtocolWriter(ConnectionContext connection, IProtocolWriter<TWriteMessage> writer)
            : this(connection, writer, new SemaphoreSlim(1))
        {
        }

        public ProtocolWriter(ConnectionContext connection, IProtocolWriter<TWriteMessage> writer, SemaphoreSlim semaphore)
        {
            Connection = connection;
            _writer = writer;
            _semaphore = semaphore;
        }

        public ConnectionContext Connection { get; }

        public async ValueTask WriteAsync(TWriteMessage protocolMessage, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);

            try
            {
                if (_disposed)
                {
                    return;
                }

                _writer.WriteMessage(protocolMessage, Connection.Transport.Output);

                var result = await Connection.Transport.Output.FlushAsync(cancellationToken);

                if (result.IsCanceled)
                {
                    throw new OperationCanceledException();
                }

                if (result.IsCompleted)
                {
                    _disposed = true;

                    await Connection.Transport.Output.CompleteAsync();
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async ValueTask WriteManyAsync(IEnumerable<TWriteMessage> protocolMessages, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken);

            try
            {
                if (_disposed)
                {
                    return;
                }

                foreach (var protocolMessage in protocolMessages)
                {
                    _writer.WriteMessage(protocolMessage, Connection.Transport.Output);
                }

                var result = await Connection.Transport.Output.FlushAsync(cancellationToken);

                if (result.IsCanceled)
                {
                    throw new OperationCanceledException();
                }

                if (result.IsCompleted)
                {
                    _disposed = true;

                    await Connection.Transport.Output.CompleteAsync();
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _semaphore.WaitAsync();

            try
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                await Connection.Transport.Output.CompleteAsync();
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
