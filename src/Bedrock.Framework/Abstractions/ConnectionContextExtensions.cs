using Bedrock.Framework.Infrastructure;
using Microsoft.AspNetCore.Connections;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework
{
    public static class ConnectionContextExtensions
    {
        public static Connection AsConnection(this ConnectionContext connectionContext) => new ConnectionContextWrapper(connectionContext);

        private class ConnectionContextWrapper : Connection, IConnectionProperties
        {
            private readonly ConnectionContext _connection;
            private readonly TaskCompletionSource<object> _executionTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            public ConnectionContextWrapper(ConnectionContext connection)
            {
                _connection = connection;
            }

            public Task ExecutionTask => _executionTcs.Task;

            protected override IDuplexPipe CreatePipe() => _connection.Transport;

            protected override Stream CreateStream() => new DuplexPipeStream(_connection.Transport.Input, _connection.Transport.Output);

            public override EndPoint LocalEndPoint => _connection.LocalEndPoint;

            public override EndPoint RemoteEndPoint => _connection.RemoteEndPoint;

            public override IConnectionProperties ConnectionProperties => this;

            protected override async ValueTask CloseAsyncCore(ConnectionCloseMethod method, CancellationToken cancellationToken)
            {
                _executionTcs.TrySetResult(null);

                switch (method)
                {
                    case ConnectionCloseMethod.Abort:
                        _connection.Abort();
                        break;
                    case ConnectionCloseMethod.Immediate:
                    case ConnectionCloseMethod.GracefulShutdown:
                        await _connection.DisposeAsync();
                        break;
                    default:
                        break;
                }
            }

            public bool TryGet(Type propertyKey, [NotNullWhen(true)] out object property)
            {
                property = _connection.Features[propertyKey];
                return property != null;
            }
        }
    }
}
