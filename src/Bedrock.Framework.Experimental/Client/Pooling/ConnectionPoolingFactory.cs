using System.Collections.Concurrent;
using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework
{
    public class ConnectionPoolingFactory : ConnectionFactory
    {
        // Represents a mapping from an individual endpoint to a pool of connections
        // Every endpoint can have multiple connections associated with it.
        private ConcurrentDictionary<EndPoint, EndPointPool> _pool;

        // The factory to create connections from
        private ConnectionFactory _innerFactory;

        public ConnectionPoolingFactory(ConnectionFactory innerFactory)
        {
            _innerFactory = innerFactory;
            _pool = new ConcurrentDictionary<EndPoint, EndPointPool>();
        }

        public override ValueTask<Connection> ConnectAsync(EndPoint endPoint, IConnectionProperties options = null, CancellationToken cancellationToken = default)
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
