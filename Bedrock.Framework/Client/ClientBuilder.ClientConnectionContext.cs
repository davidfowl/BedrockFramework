using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;

namespace Bedrock.Framework
{
    public partial class ClientBuilder
    {
        // This connection context exists solely for the purpose of being notified when DisposeAsync is called.
        private class ClientConnectionContext : ConnectionContext
        {
            private readonly ConnectionContext _connection;
            private readonly TaskCompletionSource<object> _executionTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            public ClientConnectionContext(ConnectionContext connection)
            {
                _connection = connection;
            }

            public Task ExecutionTask => _executionTcs.Task;

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

            public override ValueTask DisposeAsync()
            {
                _executionTcs.TrySetResult(null);
                return _connection.DisposeAsync();
            }
        }
    }
}
