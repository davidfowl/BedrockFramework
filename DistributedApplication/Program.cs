using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Bedrock.Framework;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DistributedApplication
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var serverId = Guid.NewGuid().ToString();

            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddConsole();
            });

            services.AddSignalR();

            var serviceProvider = services.BuildServiceProvider();

            ILogger logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(Program).FullName + "." + serverId);

            var server = new ServerBuilder(serviceProvider)
                        .UseSockets(sockets =>
                        {
                            // This is for servers to connect to
                            sockets.Listen(IPAddress.Loopback, 0, builder => builder.UseHub<ServerHub>());

                            // This is for clients
                            // sockets.Listen(IPAddress.Loopback, 0, builder => { });
                        })
                        .Build();

            await server.StartAsync();

            foreach (var ep in server.EndPoints)
            {
                logger.LogInformation("Listening on {ep}", serverId, ep);
            }

            var serversMapping = new ConcurrentDictionary<string, Server>();

            var hubConnection = new HubConnectionBuilder()
                                //.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Information).AddConsole())
                                .WithClientBuilder(new IPEndPoint(IPAddress.Loopback, 6030), builder =>
                                {
                                    builder.UseSockets().UseConnectionLogging("Client");
                                })
                                .WithAutomaticReconnect()
                                .Build();

            var serverEndPoint = server.EndPoints.Cast<IPEndPoint>().First();
            var thisServer = new Server
            {
                Id = serverId,
                Host = serverEndPoint.Address.ToString(),
                Port = serverEndPoint.Port
            };

            hubConnection.Reconnected += OnReconnected;
            hubConnection.On<Server>(nameof(Join), Join);
            hubConnection.On<Server>(nameof(Leave), Leave);

            await hubConnection.StartAsync();
            await Sync();

            var tcs = new TaskCompletionSource<object>();
            Console.CancelKeyPress += (sender, e) => tcs.TrySetResult(null);
            await tcs.Task;

            await server.StopAsync();

            async Task Sync()
            {
                var servers = await hubConnection.InvokeAsync<Server[]>("Join", thisServer);

                foreach (var server in servers)
                {
                    if (serversMapping.TryAdd(server.Id, server))
                    {
                        await OnServerAdded(server);
                    }
                }
            }

            async Task Join(Server server)
            {
                if (serversMapping.TryAdd(server.Id, server))
                {
                    await OnServerAdded(server);
                }
            }

            async Task Leave(Server server)
            {
                // We use the hub connection to detect leave
            }

            Task OnReconnected(string id)
            {
                return Sync();
            }

            async Task OnServerAdded(Server server)
            {
                try
                {
                    var connection = new HubConnectionBuilder()
                                    // .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Information).AddConsole())
                                    .WithClientBuilder(new IPEndPoint(IPAddress.Parse(server.Host), server.Port), builder =>
                                    {
                                        builder.UseSockets().UseConnectionLogging(server.Id);
                                    })
                                    .Build();

                    connection.Closed += (e) =>
                    {
                        if (serversMapping.TryRemove(server.Id, out _))
                        {
                            logger.LogInformation("Disconnected from server {server}", server);
                        }

                        return Task.CompletedTask;
                    };

                    await connection.StartAsync();

                    logger.LogInformation("Connected to server {server}", server);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unable to connect to {server}", server);

                    serversMapping.TryRemove(server.Id, out _);
                }
            }

        }

        public class ServerHub : Hub
        {
            public override Task OnConnectedAsync()
            {
                return base.OnConnectedAsync();
            }

            public override Task OnDisconnectedAsync(Exception exception)
            {
                return base.OnDisconnectedAsync(exception);
            }
        }

        public class Server
        {
            public string Id { get; set; }
            public string Host { get; set; }
            public int Port { get; set; }

            public override string ToString() => $"{Id}@{Host}:{Port}";
        }
    }
}
