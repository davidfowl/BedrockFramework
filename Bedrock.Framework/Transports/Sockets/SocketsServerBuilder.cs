using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;

namespace Bedrock.Framework
{
    public class SocketsServerBuilder
    {
        private List<(EndPoint EndPoint, Action<IConnectionBuilder> Application)> _bindings = new List<(EndPoint, Action<IConnectionBuilder>)>();

        public SocketTransportOptions Options { get; } = new SocketTransportOptions();

        public SocketsServerBuilder Listen(EndPoint endPoint, Action<IConnectionBuilder> configure)
        {
            _bindings.Add((endPoint, configure));
            return this;
        }

        public SocketsServerBuilder Listen(IPAddress address, int port, Action<IConnectionBuilder> configure)
        {
            return Listen(new IPEndPoint(address, port), configure);
        }

        public SocketsServerBuilder ListenAnyIP(int port, Action<IConnectionBuilder> configure)
        {
            return Listen(IPAddress.Any, port, configure);
        }

        public SocketsServerBuilder ListenLocalhost(int port, Action<IConnectionBuilder> configure)
        {
            return Listen(IPAddress.Loopback, port, configure);
        }

        public SocketsServerBuilder ListenUnixSocket(string socketPath, Action<IConnectionBuilder> configure)
        {
            return Listen(new UnixDomainSocketEndPoint(socketPath), configure);
        }

        internal void Apply(ServerBuilder builder)
        {
            var socketTransportFactory = new SocketTransportFactory(Microsoft.Extensions.Options.Options.Create(Options), builder.ApplicationServices.GetLoggerFactory());

            foreach (var binding in _bindings)
            {
                builder.Listen(binding.EndPoint, socketTransportFactory, binding.Application);
            }
        }
    }
}
