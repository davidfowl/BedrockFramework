using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace DistributedApplication.ServiceRegistry
{
    public class ServiceRegistryHub : Hub<IServiceRegistryClient>
    {
        private static ConcurrentDictionary<string, Server> _servers = new ConcurrentDictionary<string, Server>();
        private readonly ILogger<ServiceRegistryHub> _logger;

        public ServiceRegistryHub(ILogger<ServiceRegistryHub> logger)
        {
            _logger = logger;
        }

        public async Task<Server[]> Join(Server server)
        {
            var otherServers = _servers.Select(c => c.Value).ToArray();

            _servers[Context.ConnectionId] = server;

            _logger.LogInformation("Server registered {server}", server);

            await Clients.Others.Join(server);

            return otherServers;
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            if (_servers.TryRemove(Context.ConnectionId, out var server))
            {
                _logger.LogInformation("Server un-registered {server}", server);
            }
            else
            {
                _logger.LogInformation("Unmapped server with disconnected");
            }
            return Clients.Others.Leave(server);
        }
    }
}
