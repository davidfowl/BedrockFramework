using System;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework.Transports.Memory
{
    public class MemoryTransport : IConnectionListenerFactory, IConnectionListener, IConnectionFactory
    {
        private Channel<ConnectionContext> _acceptQueue = Channel.CreateUnbounded<ConnectionContext>();

        public EndPoint EndPoint { get; set; }

        public async ValueTask<ConnectionContext> AcceptAsync(CancellationToken cancellationToken = default)
        {
            if (await _acceptQueue.Reader.WaitToReadAsync(cancellationToken))
            {
                while (_acceptQueue.Reader.TryRead(out var item))
                {
                    return item;
                }
            }

            return null;
        }

        public ValueTask<IConnectionListener> BindAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
        {
            EndPoint = endpoint;
            return new ValueTask<IConnectionListener>(this);
        }

        public ValueTask<ConnectionContext> ConnectAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
        {
            var pair = DuplexPipe.CreateConnectionPair(new PipeOptions(), new PipeOptions());

            var serverConnection = new DefaultConnectionContext(Guid.NewGuid().ToString(), pair.Transport, pair.Application)
            {
                LocalEndPoint = endpoint,
                RemoteEndPoint = endpoint
            };

            var clientConnection = new DefaultConnectionContext(serverConnection.ConnectionId, pair.Application, pair.Transport)
            {
                LocalEndPoint = endpoint,
                RemoteEndPoint = endpoint
            };

            _acceptQueue.Writer.TryWrite(serverConnection);
            return new ValueTask<ConnectionContext>(clientConnection);
        }

        public ValueTask DisposeAsync()
        {
            return UnbindAsync(default);
        }

        public ValueTask UnbindAsync(CancellationToken cancellationToken = default)
        {
            _acceptQueue.Writer.TryComplete();

            return default;
        }
    }
}
