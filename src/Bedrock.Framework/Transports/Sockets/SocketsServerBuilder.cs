using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Bedrock.Framework
{
    public class SocketsServerBuilder
    {
        private List<(EndPoint EndPoint, int Port, Action<IConnectionBuilder> Application)> _bindings = new List<(EndPoint, int, Action<IConnectionBuilder>)>();

        public SocketsServerBuilder Listen(EndPoint endPoint, Action<IConnectionBuilder> configure)
        {
            _bindings.Add((endPoint, 0, configure));
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
            _bindings.Add((null, port, configure));
            return this;
        }

        public SocketsServerBuilder ListenUnixSocket(string socketPath, Action<IConnectionBuilder> configure)
        {
            return Listen(new UnixDomainSocketEndPoint(socketPath), configure);
        }

        internal void Apply(ServerBuilder builder)
        {
            var socketTransportFactory = new SocketsTransport();

            foreach (var binding in _bindings)
            {
                if (binding.EndPoint == null)
                {
                    var connectionBuilder = new ConnectionBuilder(builder.ApplicationServices);
                    binding.Application(connectionBuilder);
                    builder.Bindings.Add(new LocalHostBinding(binding.Port, connectionBuilder.Build(), socketTransportFactory.ConnectionListenerFactory));
                }
                else
                {

                    builder.Listen(binding.EndPoint, socketTransportFactory.ConnectionListenerFactory, binding.Application);
                }
            }
        }
    }
}
