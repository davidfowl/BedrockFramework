using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Bedrock.Framework
{
    public class AzureSignalRConnectionFactory : IConnectionFactory
    {
        private readonly ILoggerFactory _loggerFactory;

        public AzureSignalRConnectionFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public async ValueTask<ConnectionContext> ConnectAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
        {
            if (!(endpoint is AzureSignalREndPoint azEndpoint))
            {
                throw new NotSupportedException($"{endpoint} is not supported");
            }

            var options = new HttpConnectionOptions
            {
                Url = azEndpoint.Uri,
                Transports = HttpTransportType.WebSockets,
                SkipNegotiation = true
            };

            options.Headers["Authorization"] = $"Bearer {azEndpoint.AccessToken}";
            var httpConnection = new HttpConnection(options, _loggerFactory);
            await httpConnection.StartAsync();

            // The SignalR service expects the handshake in default mode, this isn't relevant when using it like a byte stream
            HandshakeProtocol.WriteRequestMessage(new HandshakeRequestMessage("unknown", 1), httpConnection.Transport.Output);
            await httpConnection.Transport.Output.FlushAsync();

            return httpConnection;
        }
    }
}
