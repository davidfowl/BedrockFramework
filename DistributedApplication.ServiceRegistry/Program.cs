using System;
using System.Net;
using System.Threading.Tasks;
using Bedrock.Framework;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DistributedApplication.ServiceRegistry
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddConsole();
            });

            // Using SignalR for RPC
            services.AddSignalR();

            var serviceProvider = services.BuildServiceProvider();

            ILogger logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<Program>();

            var server = new ServerBuilder(serviceProvider)
                        .UseSockets(sockets =>
                        {
                            sockets.Listen(IPAddress.Loopback, 6030, builder =>
                            {
                                builder.UseConnectionLogging()
                                       .UseHub<ServiceRegistryHub>();
                            });
                        })
                        .Build();

            await server.StartAsync();

            foreach (var ep in server.EndPoints)
            {
                logger.LogInformation("Listening on {ep}", ep);
            }

            var tcs = new TaskCompletionSource<object>();
            Console.CancelKeyPress += (sender, e) => tcs.TrySetResult(null);
            await tcs.Task;

            await server.StopAsync();
        }
        private class ConnectionEndPointFeature : IConnectionEndPointFeature
        {
            public EndPoint LocalEndPoint { get; set; }
            public EndPoint RemoteEndPoint { get; set; }
        }
    }
}
