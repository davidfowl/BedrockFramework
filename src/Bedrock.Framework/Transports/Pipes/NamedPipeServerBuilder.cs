using Microsoft.AspNetCore.Connections;

using System;
using System.Collections.Generic;
using System.Net;

namespace Bedrock.Framework
{
    public class NamedPipeServerBuilder
    {
        private List<(EndPoint EndPoint, Action<IConnectionBuilder> Application)> _bindings = new List<(EndPoint, Action<IConnectionBuilder>)>();

        public NamedPipeTransportOptions Options { get; } = new NamedPipeTransportOptions();

        public NamedPipeServerBuilder Listen(NamedPipeEndPoint endPoint, Action<IConnectionBuilder> configure)
        {
            _bindings.Add((endPoint, configure));
            return this;
        }

        public NamedPipeServerBuilder Listen(string pipeName, string serverName, Action<IConnectionBuilder> configure)
        {
            return Listen(new NamedPipeEndPoint(pipeName, serverName), configure);
        }

        public NamedPipeServerBuilder Listen(string pipeName, Action<IConnectionBuilder> configure)
        {
            return Listen(new NamedPipeEndPoint(pipeName), configure);
        }

        internal void Apply(ServerBuilder builder)
        {
            var namedPipeTransportFactory = new NamedPipeTransportFactory(Microsoft.Extensions.Options.Options.Create(Options), builder.ApplicationServices.GetLoggerFactory());

            foreach (var binding in _bindings)
            {
                if (binding.EndPoint == null)
                {
                    var connectionBuilder = new ConnectionBuilder(builder.ApplicationServices);
                    binding.Application(connectionBuilder);
                    builder.Bindings.Add(new NamedPipeHostBinding(binding.EndPoint, connectionBuilder.Build(), namedPipeTransportFactory));
                }
                else
                {
                    builder.Listen(binding.EndPoint, namedPipeTransportFactory, binding.Application);
                }
            }
        }
    }
}