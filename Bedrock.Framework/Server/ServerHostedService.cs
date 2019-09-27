using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bedrock.Framework
{
    public class ServerHostedService : IHostedService
    {
        private readonly Server _server;

        public ServerHostedService(ILoggerFactory loggerFactory, IOptions<ServerOptions> options)
        {
            _server = new Server(loggerFactory, options.Value);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return _server.StartAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _server.StopAsync(cancellationToken);
        }
    }
}
