using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace BedrockTransports
{
    public class EchoServer : ConnectionHandler
    {
        public override Task OnConnectedAsync(ConnectionContext connection)
        {
            return connection.Transport.Input.CopyToAsync(connection.Transport.Output);
        }
    }
}
