using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework
{
    public partial class ConnectionPool : IConnectionPool, IAsyncDisposable
    {
        // Represents a mapping from an individual endpoint to a pool of connections
        // Every endpoint can have multiple connections associated with it.
        private ConcurrentDictionary<EndPoint, EndPointPool> _pool;

        // The factory to create connections from
        private IConnectionFactory _connectionFactory;

        public ConnectionPool(IConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
            _pool = new ConcurrentDictionary<EndPoint, EndPointPool>();
        }
        
        public ValueTask DisposeAsync()
        {
            // Dispose all connections
            return default;
        }

        public ValueTask<ConnectionContext> GetConnectionAsync(EndPoint endPoint, CancellationToken cancellationToken = default)
        {
            if (!_pool.TryGetValue(endPoint, out var endPointPool))
            {
                endPointPool = new EndPointPool(endPoint, _connectionFactory, endPoint is HttpEndPoint ? ((HttpEndPoint)endPoint).MaxConnections : 1);
                _pool[endPoint] = endPointPool;
            }

            return endPointPool.GetConnectionAsync(cancellationToken);
        }
    }
}