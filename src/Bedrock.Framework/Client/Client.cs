using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework
{
    public class Client : ConnectionFactory
    {
        private readonly ConnectionFactory _connectionFactory;
        private readonly ConnectionDelegate _application;

        public Client(ConnectionFactory connectionFactory, ConnectionDelegate application)
        {
            _connectionFactory = connectionFactory;
            _application = application;
        }

        public override async ValueTask<Connection> ConnectAsync(EndPoint endPoint, IConnectionProperties options = null, CancellationToken cancellationToken = default)
        {
            var connection = await _connectionFactory.ConnectAsync(endPoint, options, cancellationToken).ConfigureAwait(false);

            // Since nothing is being returned from this middleware, we need to wait for the last middleware to run
            // until we yield this call. Stash a tcs in the items bag that allows this code to get notified
            // when the middleware ran
            var connectionContextWithDelegate = new ConnectionContextWithDelegate(connection, _application);

            // Execute the middleware pipeline
            connectionContextWithDelegate.Start();

            // Wait for it the most inner middleware to run
            return await connectionContextWithDelegate.Initialized.Task.ConfigureAwait(false);
        }
    }
}
