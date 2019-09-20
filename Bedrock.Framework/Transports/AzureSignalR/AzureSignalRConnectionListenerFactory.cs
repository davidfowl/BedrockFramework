using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace Bedrock.Framework
{
    public class AzureSignalRConnectionListenerFactory : IConnectionListenerFactory
    {
        private readonly ILoggerFactory _loggerFactory;

        public AzureSignalRConnectionListenerFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public async ValueTask<IConnectionListener> BindAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
        {
            if (!(endpoint is AzureSignalREndPoint azEndpoint))
            {
                throw new NotSupportedException($"{endpoint} is not supported");
            }

            var listener = new AzureSignalRConnectionListener(azEndpoint.Uri, azEndpoint.AccessToken, _loggerFactory)
            {
                EndPoint = endpoint
            };
            await listener.StartAsync();
            return listener;
        }
    }
}
