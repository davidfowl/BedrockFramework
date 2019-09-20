using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BedrockTransports
{
    public class ServerApplication : BackgroundService
    {
        private readonly ServerApplicationOptions _serverOptions;
        private readonly ILogger<ServerApplication> _logger;

        public ServerApplication(ILogger<ServerApplication> logger, IOptions<ServerApplicationOptions> options)
        {
            _logger = logger;
            _serverOptions = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var tasks = new List<Task>(_serverOptions.Bindings.Count);
            foreach (var binding in _serverOptions.Bindings)
            {
                var listener = await binding.ConnectionListenerFactory.BindAsync(binding.EndPoint);
                _logger.LogInformation("Listening on {address}", binding.EndPoint);

                tasks.Add(RunServerAsync(listener, binding.ServerApplication, stoppingToken));
            }

            await Task.WhenAll(tasks);
        }

        public static async Task RunClientAsync(ConnectionContext connection, ConnectionDelegate connectionDelegate, CancellationToken cancellationToken = default)
        {
            await connectionDelegate(connection);
        }

        public static async Task RunServerAsync(IConnectionListener listener, ConnectionDelegate connectionDelegate, CancellationToken cancellationToken = default)
        {
            while (true)
            {
                var connection = await listener.AcceptAsync(cancellationToken);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await connectionDelegate(connection);
                    }
                    finally
                    {
                        await connection.DisposeAsync();
                    }
                });
            }
        }
    }
}
