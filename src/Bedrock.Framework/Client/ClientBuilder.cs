using System;
using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;
using Bedrock.Framework.Infrastructure;

namespace Bedrock.Framework
{
    public partial class ClientBuilder : IConnectionBuilder
    {
        private readonly ConnectionBuilder _connectionBuilder;

        public ClientBuilder() : this(EmptyServiceProvider.Instance)
        {

        }

        public ClientBuilder(IServiceProvider serviceProvider)
        {
            _connectionBuilder = new ConnectionBuilder(serviceProvider);
        }

        internal static object Key { get; } = new object();

        private ConnectionFactory ConnectionFactory { get; set; } = new ThrowConnectionFactory();

        public IServiceProvider ApplicationServices => _connectionBuilder.ApplicationServices;

        public Client Build()
        {
            // Middleware currently a single linear execution flow without a return value.
            // We need to return the connection when it reaches the innermost middleware (D in this case)
            // Then we need to wait until dispose is called to unwind that pipeline.

            // A -> 
            //      B -> 
            //           C -> 
            //                D
            //           C <-
            //      B <-
            // A <-

            _connectionBuilder.Run(connection =>
            {
                if (connection.ConnectionProperties.TryGet<ConnectionContextWithDelegate>(out var connectionContextWithDelegate))
                {
                    connectionContextWithDelegate.Initialized.TrySetResult(connection);


                    // This task needs to stay around until the connection is disposed
                    // only then can we unwind the middleware chain
                    return connectionContextWithDelegate.ExecutionTask;
                }

                // REVIEW: Do we throw in this case? It's edgy but possible to call next with a differnt
                // connection delegate that originally given
                return Task.CompletedTask;
            });

            var application = _connectionBuilder.Build();

            return new Client(ConnectionFactory, application);
        }

        public ClientBuilder UseConnectionFactory(ConnectionFactory connectionFactory)
        {
            ConnectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            return this;
        }

        public ClientBuilder Use(Func<ConnectionFactory, ConnectionFactory> middleware)
        {
            ConnectionFactory = middleware(ConnectionFactory);
            return this;
        }

        public IConnectionBuilder Use(Func<ConnectionDelegate, ConnectionDelegate> middleware)
        {
            return _connectionBuilder.Use(middleware);
        }

        ConnectionDelegate IConnectionBuilder.Build()
        {
            return _connectionBuilder.Build();
        }

        private class ThrowConnectionFactory : ConnectionFactory
        {
            public override ValueTask<Connection> ConnectAsync(EndPoint endPoint, IConnectionProperties options = null, CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException("No transport configured. Set the ConnectionFactory property.");
            }
        }
    }
}
