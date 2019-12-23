using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bedrock.Framework
{
    public partial class WebSocketConnectionListenerFactory : IConnectionListenerFactory, IHostApplicationLifetime
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly Action<Microsoft.AspNetCore.Http.Connections.WebSocketOptions> _configure;

        public WebSocketConnectionListenerFactory(ILoggerFactory loggerFactory, Action<Microsoft.AspNetCore.Http.Connections.WebSocketOptions> configure = null)
        {
            _loggerFactory = loggerFactory;
            _configure = configure ?? new Action<Microsoft.AspNetCore.Http.Connections.WebSocketOptions>(o => { });
        }

        public CancellationToken ApplicationStarted => default;

        public CancellationToken ApplicationStopped => default;

        public CancellationToken ApplicationStopping => default;

        public async ValueTask<IConnectionListener> BindAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
        {
            IPEndPoint iPEndPoint = null;
            var path = "";
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
                    path = uriEndPoint.Uri.LocalPath != "/" ? uriEndPoint.Uri.LocalPath : "";
                    break;
                case IPEndPoint ip:
                    iPEndPoint = ip;
                    break;
                default:
                    throw new NotSupportedException($"{endpoint} not supported");
            }

            var services = new ServiceCollection();
            services.AddOptions();
            services.AddSingleton(_loggerFactory);
            services.AddLogging();
            services.AddConnections();
            services.AddSingleton<IHostApplicationLifetime>(this);
            var diagnosticListener = new DiagnosticListener("WebSocketServer");
            services.AddSingleton(diagnosticListener);
            var serverOptions = Options.Create(new KestrelServerOptions() { ApplicationServices = services.BuildServiceProvider() }); ;
            var socketOptions = Options.Create(new SocketTransportOptions());
            var server = new KestrelServer(serverOptions, new SocketTransportFactory(socketOptions, _loggerFactory), _loggerFactory);
            ListenOptions listenOptions = null;

            // Bind an HTTP/1 endpoint
            server.Options.Listen(iPEndPoint, o =>
            {
                o.UseHttps();
                o.Protocols = HttpProtocols.Http1;
                // Storing the options so we can get the resolved EndPoint later
                listenOptions = o;
            });

            var listener = new WebSocketConnectionListener(server, _configure, serverOptions.Value.ApplicationServices, path);

            await listener.BindAsync(cancellationToken);

            listener.EndPoint = listenOptions.IPEndPoint;

            return listener;
        }

        public void StopApplication()
        {
            // Shut the server down
        }
    }
}
