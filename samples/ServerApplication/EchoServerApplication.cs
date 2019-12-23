using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bedrock.Framework.Middleware.Tls;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace Bedrock.Framework
{
    public class EchoServerApplication : ConnectionHandler
    {
        private readonly ILogger<EchoServerApplication> _logger;

        public EchoServerApplication(ILogger<EchoServerApplication> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync(ConnectionContext connection)
        {
            try
            {
                _logger.LogInformation("{ConnectionId} connected", connection.ConnectionId);

                var handshake = connection.Features.Get<ITlsHandshakeFeature>();
                
                if (handshake != null)
                {
                    _logger.LogInformation("TLS enabled, TLS Verson={TLSVersion}, HashAlgorithm={HashAlgorithm}", handshake.Protocol, handshake.HashAlgorithm);
                }

                await connection.Transport.Input.CopyToAsync(connection.Transport.Output);
            }
            finally
            {
                _logger.LogInformation("{ConnectionId} disconnected", connection.ConnectionId);
            }
        }
    }
}
