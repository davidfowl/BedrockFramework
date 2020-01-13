using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework
{
    public class ConnectionPoolingFactory : IConnectionFactory
    {
        // Represents a mapping from an individual endpoint to a pool of connections
        // Every endpoint can have multiple connections associated with it.
        private ConcurrentDictionary<EndPoint, EndPointPool> _pool;

        // The factory to create connections from
        private IConnectionFactory _innerFactory;

        public ConnectionPoolingFactory(IConnectionFactory innerFactory)
        {
            _innerFactory = innerFactory;
            _pool = new ConcurrentDictionary<EndPoint, EndPointPool>();
        }

        public ValueTask DisposeAsync()
        {
            // Dispose all connections
            return default;
        }

        public ValueTask<ConnectionContext> ConnectAsync(EndPoint endPoint, CancellationToken cancellationToken = default)
        {
            var endPointPool = _pool.GetOrAdd(
                endPoint,
                (ep, factory) =>
                {
                    var maxConnections = ep is IMaxConnectionFeature maxConnectionEndpoint ? maxConnectionEndpoint.MaxConnections : 1;
                    return new EndPointPool(ep, factory, maxConnections);
                }, _innerFactory);

            return endPointPool.GetConnectionAsync(cancellationToken);
        }
    }
}
