using System;
using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework
{
    internal class PooledConnection : Connection, IAsyncDisposable
    {
        private Connection _connection;
        private IEndPointPool _pool;

        public PooledConnection(Connection context, IEndPointPool pool)
        {
            _connection = context;
            _pool = pool;
        }

        public override IConnectionProperties ConnectionProperties => _connection.ConnectionProperties;

        public override EndPoint LocalEndPoint => _connection.LocalEndPoint;

        public override EndPoint RemoteEndPoint => _connection.RemoteEndPoint;

        protected override async ValueTask CloseAsyncCore(ConnectionCloseMethod method, CancellationToken cancellationToken)
        {
            switch (method)
            {
                case ConnectionCloseMethod.GracefulShutdown:
                    await _pool.ReturnAsync(_connection);
                    break;
                case ConnectionCloseMethod.Abort:
                    await _connection.CloseAsync(method);
                    break;
                case ConnectionCloseMethod.Immediate:
                    await _connection.CloseAsync(method);
                    break;
                default:
                    break;
            }
        }
    }
}
