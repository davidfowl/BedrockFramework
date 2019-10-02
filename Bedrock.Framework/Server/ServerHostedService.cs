using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Bedrock.Framework
{
    public class ServerHostedService : IHostedService
    {
        private readonly Server _server;

        public ServerHostedService(IOptions<ServerHostedServiceOptions> options)
        {
            _server = options.Value.ServerBuilder.Build();
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
