using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace BedrockTransports
{
    public class EchoServer : ConnectionHandler
    {
        private readonly ILogger<EchoServer> _logger;

        public EchoServer(ILogger<EchoServer> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync(ConnectionContext connection)
        {
            try
            {
                _logger.LogInformation("{ConnectionId} connected", connection.ConnectionId);

                await connection.Transport.Input.CopyToAsync(connection.Transport.Output);
            }
            finally
            {
                _logger.LogInformation("{ConnectionId} disconnected", connection.ConnectionId);
            }
        }
    }
}
