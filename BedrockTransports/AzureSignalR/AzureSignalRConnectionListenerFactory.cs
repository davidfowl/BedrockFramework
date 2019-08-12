using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace BedrockTransports
{
    public class AzureSignalRConnectionListenerFactory : IConnectionListenerFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly bool _isNewEndpoint;

        public AzureSignalRConnectionListenerFactory(ILoggerFactory loggerFactory, bool isNewEndpoint)
        {
            _loggerFactory = loggerFactory;
            _isNewEndpoint = isNewEndpoint;
        }

        public async ValueTask<IConnectionListener> BindAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
        {
            if (!(endpoint is AzureSignalREndPoint azEndpoint))
            {
                throw new NotSupportedException($"{endpoint} is not supported");
            }

            var listener = new AzureSignalRConnectionListener(azEndpoint.Uri, azEndpoint.AccessToken, _loggerFactory, _isNewEndpoint)
            {
                EndPoint = endpoint
            };
            await listener.StartAsync();
            return listener;
        }
    }
}
