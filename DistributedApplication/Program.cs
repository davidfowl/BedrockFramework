using System;
using System.Net;
using System.Threading.Tasks;
using Bedrock.Framework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DistributedApplication
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddConsole();
            });

            var serviceProvider = services.BuildServiceProvider();

            var server = new ServerBuilder(serviceProvider)
                        .UseSockets(sockets =>
                        {
                            sockets.Listen(IPAddress.Loopback, 0, builder => { });
                        })
                        .Build();

            await server.StartAsync();

            
            foreach (var ep in server.EndPoints)
            {
                Console.WriteLine($"Listening on {ep}");
            }

            var tcs = new TaskCompletionSource<object>();
            Console.CancelKeyPress += (sender, e) => tcs.TrySetResult(null);
            await tcs.Task;

            await server.StopAsync();
        }
    }
}
