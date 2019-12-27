﻿using System.Threading.Tasks;
using Bedrock.Framework.Protocols;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using Protocols;

namespace ServerApplication
{
    public class MyCustomProtocol : ConnectionHandler
    {
        private readonly ILogger _logger;

        public MyCustomProtocol(ILogger<MyCustomProtocol> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync(ConnectionContext connection)
        {
            // Use a length prefixed protocol
            var protocol = new LengthPrefixedProtocol();
            var reader = connection.CreateReader();
            var writer = connection.CreateWriter();

            while (true)
            {
                try
                {
                    var result = await reader.ReadAsync(protocol);
                    var message = result.Message;

                    _logger.LogInformation("Received a message of {Length} bytes", message.Payload.Length);

                    if (result.IsCompleted)
                    {
                        break;
                    }

                    await writer.WriteAsync(protocol, message);
                }
                finally
                {
                    reader.Advance();
                }
            }
        }
    }
}