using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework
{
    public class Client : IConnectionFactory
    {
        private readonly IConnectionFactory _connectionFactory;
        private readonly ConnectionDelegate _application;

        public Client(IConnectionFactory connectionFactory, ConnectionDelegate application)
        {
            _connectionFactory = connectionFactory;
            _application = application;
        }

        public async ValueTask<ConnectionContext> ConnectAsync(EndPoint endPoint, CancellationToken cancellationToken = default)
        {
            var connection = await _connectionFactory.ConnectAsync(endPoint, cancellationToken);

            // Since nothing is being returned from this middleware, we need to wait for the last middleware to run
            // until we yield this call. Stash a tcs in the items bag that allows this code to get notified
            // when the middleware ran
            var clientConnectionContext = new ClientConnectionContext(connection, _application);


            return await clientConnectionContext.Initialized.Task;
        }
    }
}
