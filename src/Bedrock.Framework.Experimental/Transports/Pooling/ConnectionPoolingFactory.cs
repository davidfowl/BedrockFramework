using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework
{
    public class ConnectionPoolingFactory : IConnectionFactory
    {
        private IConnectionPool _pool;
        
        public ConnectionPoolingFactory(IConnectionPool pool)
        {
            _pool = pool;
        }

        public ValueTask<ConnectionContext> ConnectAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
        {
            return _pool.GetConnectionAsync(endpoint, cancellationToken);
        }
    }
}