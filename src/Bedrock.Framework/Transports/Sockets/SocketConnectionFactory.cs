using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework
{
    public class SocketConnectionFactory : IConnectionFactory
    {
        private readonly SocketType _socketType;
        private readonly ProtocolType _protocolType;

        public SocketConnectionFactory(SocketType socketType, ProtocolType protocolType)
        {
            _socketType = socketType;
            _protocolType = protocolType;
        }

        public ValueTask<ConnectionContext> ConnectAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
        {
            return new SocketConnection(endpoint, _socketType, _protocolType).StartAsync();
        }
    }
}
