using System;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework.Transports.Memory
{
    public partial class MemoryTransport : IConnectionListenerFactory, IConnectionFactory
    {
        private readonly ConcurrentDictionary<EndPoint, MemoryConnectionListener> _listeners = new ConcurrentDictionary<EndPoint, MemoryConnectionListener>();

        public ValueTask<IConnectionListener> BindAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
        {
            endpoint ??= MemoryEndPoint.Default;

            if (_listeners.TryGetValue(endpoint, out _))
            {
                throw new AddressInUseException($"{endpoint} listener already bound");
            }

            MemoryConnectionListener listener;
            _listeners[endpoint] = listener = new MemoryConnectionListener() { EndPoint = endpoint };
            return new ValueTask<IConnectionListener>(listener);
        }

        public ValueTask<ConnectionContext> ConnectAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
        {
            endpoint ??= MemoryEndPoint.Default;

            if (!_listeners.TryGetValue(endpoint, out var listener))
            {
                throw new InvalidOperationException($"{endpoint} not bound!");
            }

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

            listener.AcceptQueue.Writer.TryWrite(serverConnection);
            return new ValueTask<ConnectionContext>(clientConnection);
        }

        private class MemoryConnectionListener : IConnectionListener
        {
            public EndPoint EndPoint { get; set; }

            internal Channel<ConnectionContext> AcceptQueue { get; } = Channel.CreateUnbounded<ConnectionContext>();

            public async ValueTask<ConnectionContext> AcceptAsync(CancellationToken cancellationToken = default)
            {
                if (await AcceptQueue.Reader.WaitToReadAsync(cancellationToken))
                {
                    while (AcceptQueue.Reader.TryRead(out var item))
                    {
                        return item;
                    }
                }

                return null;
            }

            public ValueTask DisposeAsync()
            {
                return UnbindAsync(default);
            }

            public ValueTask UnbindAsync(CancellationToken cancellationToken = default)
            {
                AcceptQueue.Writer.TryComplete();

                return default;
            }
        }
    }
}
