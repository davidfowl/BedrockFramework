using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Connections;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
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

        public override async Task OnConnectedAsync(Connection connection)
        {
            try
            {
                _logger.LogInformation("{ConnectionId} connected", connection.RemoteEndPoint);

                if (connection.ConnectionProperties.TryGet<ITlsHandshakeFeature>(out var handshake))
                {
                    _logger.LogInformation("TLS enabled, TLS Verson={TLSVersion}, HashAlgorithm={HashAlgorithm}", handshake.Protocol, handshake.HashAlgorithm);
                }

                await connection.Pipe.Input.CopyToAsync(connection.Pipe.Output);
            }
            finally
            {
                _logger.LogInformation("{ConnectionId} disconnected", connection.RemoteEndPoint);
            }
        }
    }
}
