using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;

namespace Bedrock.Framework
{
    internal class PooledConnectionContext : ConnectionContext, IAsyncDisposable
    {
        private ConnectionContext _connection;
        private IEndPointPool _pool;

        public PooledConnectionContext(ConnectionContext context, IEndPointPool pool)
        {
            _connection = context;
            _pool = pool;
        }

        public override string ConnectionId
        {
            get => _connection.ConnectionId;
            set => _connection.ConnectionId = value;
        }

        public override IFeatureCollection Features => _connection.Features;

        public override IDictionary<object, object> Items
        {
            get => _connection.Items;
            set => _connection.Items = value;
        }

        public override IDuplexPipe Transport
        {
            get => _connection.Transport;
            set => _connection.Transport = value;
        }

        public override EndPoint LocalEndPoint
        {
            get => _connection.LocalEndPoint;
            set => _connection.LocalEndPoint = value;
        }

        public override EndPoint RemoteEndPoint
        {
            get => _connection.RemoteEndPoint;
            set => _connection.RemoteEndPoint = value;
        }

        public override CancellationToken ConnectionClosed
        {
            get => _connection.ConnectionClosed;
            set => _connection.ConnectionClosed = value;
        }

        public override void Abort()
        {
            _connection.Abort();
        }

        public override void Abort(ConnectionAbortedException abortReason)
        {
            _connection.Abort(abortReason);
        }

        public override async ValueTask DisposeAsync()
        {
            await _pool.ReturnAsync(_connection);
        }
    }
}
