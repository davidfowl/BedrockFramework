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
    internal class ConnectionContextWithDelegate : Connection, IConnectionProperties
    {
        private readonly Connection _connection;
        private readonly TaskCompletionSource<object> _executionTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        private Task _middlewareTask;
        private ConnectionDelegate _connectionDelegate;

        public ConnectionContextWithDelegate(Connection connection, ConnectionDelegate connectionDelegate)
        {
            _connection = connection;
            _connectionDelegate = connectionDelegate;
        }

        // Execute the middleware pipeline
        public void Start()
        {
            _middlewareTask = RunMiddleware();
        }

        private async Task RunMiddleware()
        {
            try
            {
                await _connectionDelegate(this).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // If we failed to initialize, bubble that exception to the caller.
                // If we've already initailized then this will noop
                Initialized.TrySetException(ex);
            }
        }

        public TaskCompletionSource<Connection> Initialized { get; set; } = new TaskCompletionSource<Connection>();

        public Task ExecutionTask => _executionTcs.Task;

        protected override IDuplexPipe CreatePipe()
        {
            return _connection.Pipe;
        }

        protected override Stream CreateStream()
        {
            return _connection.Stream;
        }

        public override EndPoint RemoteEndPoint => _connection.RemoteEndPoint;
        public override EndPoint LocalEndPoint => _connection.LocalEndPoint;
        public override IConnectionProperties ConnectionProperties => this;

        protected override async ValueTask CloseAsyncCore(ConnectionCloseMethod method, CancellationToken cancellationToken)
        {
            await _connection.CloseAsync(method, cancellationToken).ConfigureAwait(false);

            _executionTcs.TrySetResult(null);

            await _middlewareTask.ConfigureAwait(false);
        }

        public bool TryGet(Type propertyKey, [NotNullWhen(true)] out object property)
        {
            if (propertyKey == typeof(ConnectionContextWithDelegate))
            {
                property = this;
                return true;
            }

            return _connection.ConnectionProperties.TryGet(propertyKey, out property);
        }
    }
}
