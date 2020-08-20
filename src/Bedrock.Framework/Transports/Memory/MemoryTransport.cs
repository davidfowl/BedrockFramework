using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework.Transports.Memory
{
    public class MemoryTransport
    {
        private readonly ConcurrentDictionary<EndPoint, MemoryConnectionListener> _listeners = new ConcurrentDictionary<EndPoint, MemoryConnectionListener>();

        public MemoryTransport()
        {
            ConnectionListenerFactory = new MemoryConnectionListenerFactory(this);
            ConnectionFactory = new MemoryConnectionFactory(this);
        }

        public ConnectionListenerFactory ConnectionListenerFactory { get; }
        public ConnectionFactory ConnectionFactory { get; }

        internal ValueTask<ConnectionListener> ListenAsync(EndPoint endPoint, IConnectionProperties options = null, CancellationToken cancellationToken = default)
        {
            MemoryConnectionListener listener;

            if (_listeners.TryGetValue(endPoint, out _) ||
                !_listeners.TryAdd(endPoint, listener = new MemoryConnectionListener(endPoint)))
            {
                throw new IOException($"{endPoint} listener already bound");
            }

            return new ValueTask<ConnectionListener>(listener);
        }

        internal ValueTask<Connection> ConnectAsync(EndPoint endPoint, IConnectionProperties options, CancellationToken cancellationToken)
        {
            if (!_listeners.TryGetValue(endPoint, out var listener) && !(endPoint is DnsEndPoint dns && _listeners.TryGetValue(new MemoryEndPoint(dns.Host), out listener)))
            {
                throw new InvalidOperationException($"{endPoint} not bound!");
            }

            var pair = DuplexPipe.CreateConnectionPair(new PipeOptions(), new PipeOptions());

            var serverConnection = Connection.FromPipe(pair.Transport, localEndPoint: endPoint, remoteEndPoint: endPoint);
            var clientConnection = Connection.FromPipe(pair.Application, localEndPoint: endPoint, remoteEndPoint: endPoint);

            listener.AcceptQueue.Writer.TryWrite(serverConnection);
            return new ValueTask<Connection>(clientConnection);
        }
    }

    public class MemoryConnectionListenerFactory : ConnectionListenerFactory
    {
        private readonly MemoryTransport _memoryTransport;

        public MemoryConnectionListenerFactory(MemoryTransport memoryTransport)
        {
            _memoryTransport = memoryTransport;
        }

        public override ValueTask<ConnectionListener> ListenAsync(EndPoint endPoint, IConnectionProperties options = null, CancellationToken cancellationToken = default)
        {
            return _memoryTransport.ListenAsync(endPoint, options, cancellationToken);
        }
    }

    public class MemoryConnectionListener : ConnectionListener, IConnectionProperties
    {
        internal Channel<Connection> AcceptQueue { get; } = Channel.CreateUnbounded<Connection>();

        public MemoryConnectionListener(EndPoint endPoint)
        {
            LocalEndPoint = endPoint;
        }

        public override IConnectionProperties ListenerProperties => this;

        public override EndPoint LocalEndPoint { get; }

        public override async ValueTask<Connection> AcceptAsync(IConnectionProperties options = null, CancellationToken cancellationToken = default)
        {
            if (await AcceptQueue.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (AcceptQueue.Reader.TryRead(out var item))
                {
                    return item;
                }
            }

            return null;
        }

        public bool TryGet(Type propertyKey, [NotNullWhen(true)] out object property)
        {
            property = null;
            return false;
        }
    }

    public class MemoryConnectionFactory : ConnectionFactory
    {
        private readonly MemoryTransport _memoryTransport;
        public MemoryConnectionFactory(MemoryTransport memoryTransport)
        {
            _memoryTransport = memoryTransport;
        }

        public override ValueTask<Connection> ConnectAsync(EndPoint endPoint, IConnectionProperties options = null, CancellationToken cancellationToken = default)
        {
            return _memoryTransport.ConnectAsync(endPoint, options, cancellationToken);
        }
    }
}
