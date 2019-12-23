using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.Extensions.Logging;

namespace Bedrock.Framework
{
    public class WebSocketConnectionFactory : IConnectionFactory
    {
        private readonly ILoggerFactory _loggerFactory;

        public WebSocketConnectionFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public async ValueTask<ConnectionContext> ConnectAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
        {
            if (!(endpoint is UriEndPoint uriEndpoint))
            {
                throw new NotSupportedException($"{endpoint} is not supported");
            }

            var options = new HttpConnectionOptions
            {
                Url = uriEndpoint.Uri,
                Transports = HttpTransportType.WebSockets,
                SkipNegotiation = true
            };

            var httpConnection = new HttpConnection(options, _loggerFactory);
            await httpConnection.StartAsync();
            return httpConnection;
        }
    }
}
