using System;
using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.Extensions.Logging;

namespace Bedrock.Framework
{
    public class WebSocketConnectionFactory : ConnectionFactory
    {
        private readonly ILoggerFactory _loggerFactory;

        public WebSocketConnectionFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public override async ValueTask<Connection> ConnectAsync(EndPoint endPoint, IConnectionProperties options = null, CancellationToken cancellationToken = default)
        {
            if (!(endPoint is UriEndPoint uriEndpoint))
            {
                throw new NotSupportedException($"{endPoint} is not supported");
            }

            var httpOptions = new HttpConnectionOptions
            {
                Url = uriEndpoint.Uri,
                Transports = HttpTransportType.WebSockets,
                SkipNegotiation = true
            };

            var httpConnection = new HttpConnection(httpOptions, _loggerFactory);
            await httpConnection.StartAsync();
            return httpConnection.AsConnection();
        }
    }
}
