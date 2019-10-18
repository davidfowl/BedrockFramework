using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework
{
    public partial class ClientBuilder : IConnectionBuilder
    {
        private readonly ConnectionBuilder _connectionBuilder;


        public ClientBuilder(IServiceProvider serviceProvider)
        {
            _connectionBuilder = new ConnectionBuilder(serviceProvider);
        }

        internal static object Key { get; } = new object();

        public IConnectionFactory ConnectionFactory { get; set; }

        internal ConnectionDelegate Application { get; set; }

        public IServiceProvider ApplicationServices => _connectionBuilder.ApplicationServices;

        public Client Build()
        {
            if (Application == null)
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
                    if (connection is ClientConnectionContext clientConnection)
                    {
                        clientConnection.Initialized.TrySetResult(clientConnection);


                        // This task needs to stay around until the connection is disposed
                        // only then can we unwind the middleware chain
                        return clientConnection.ExecutionTask;
                    }

                    // REVIEW: Do we throw in this case? It's edgy but possible to call next with a differnt
                    // connection delegate that originally given
                    return Task.CompletedTask;
                });

                Application = _connectionBuilder.Build();
            }

            return new Client(this);
        }

        public IConnectionBuilder Use(Func<ConnectionDelegate, ConnectionDelegate> middleware)
        {
            return _connectionBuilder.Use(middleware);
        }

        ConnectionDelegate IConnectionBuilder.Build()
        {
            return _connectionBuilder.Build();
        }
    }
}
