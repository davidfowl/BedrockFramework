using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bedrock.Framework
{
    public partial class Http2ConnectionListenerFactory : IConnectionListenerFactory
    {
        private readonly ILoggerFactory _loggerFactory;

        public Http2ConnectionListenerFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public async ValueTask<IConnectionListener> BindAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
        {
            IPEndPoint iPEndPoint = null;
            switch (endpoint)
            {
                // Kestrel doesn't natively support UriEndpoints as yet
                case UriEndPoint uriEndPoint:
                    IPAddress address = null;
                    if (uriEndPoint.Uri.Host == "localhost")
                    {
                        address = IPAddress.Loopback;
                    }
                    else
                    {
                        IPAddress.Parse(uriEndPoint.Uri.Host);
                    }
                    iPEndPoint = new IPEndPoint(address, uriEndPoint.Uri.Port);
                    break;
                case IPEndPoint ip:
                    iPEndPoint = ip;
                    break;
                default:
                    throw new NotSupportedException($"{endpoint} not supported");
            }

            var services = new ServiceCollection();
            services.AddSingleton(_loggerFactory);
            services.AddLogging();
            var serverOptions = Options.Create(new KestrelServerOptions() { ApplicationServices = services.BuildServiceProvider() }); ;
            var socketOptions = Options.Create(new SocketTransportOptions());
            var socketTransportFactory = new SocketTransportFactory(socketOptions, _loggerFactory);
            var server = new KestrelServer(serverOptions, socketTransportFactory, _loggerFactory);
            ListenOptions listenOptions = null;

            // Bind an HTTP/2 endpoint
            server.Options.Listen(iPEndPoint, options =>
            {
                options.UseHttps();
                options.Protocols = HttpProtocols.Http2;
                // Storing the options so we can get the resolved EndPoint later
                listenOptions = options;
            });

            var listener = new Http2ConnectionListener(server);

            await listener.BindAsync(cancellationToken);

            listener.EndPoint = listenOptions.IPEndPoint;

            return listener;
        }
    }
}
