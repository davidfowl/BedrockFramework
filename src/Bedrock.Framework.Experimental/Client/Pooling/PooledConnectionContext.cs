using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;

namespace Bedrock.Framework
{
    internal class PooledConnectionContext : Connection, IAsyncDisposable
    {
        private Connection _connection;
        private IEndPointPool _pool;

        public PooledConnectionContext(Connection context, IEndPointPool pool)
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
