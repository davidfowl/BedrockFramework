using System;
using System.Net;
using System.Threading.Tasks;
using Bedrock.Framework;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ServerApplication
{
    public partial class Program
    {
        public static async Task Main(string[] args)
        {
            // Manual wire up of the server
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddConsole();
            });

            services.AddSignalR();

            var serviceProvider = services.BuildServiceProvider();

            var server = new ServerBuilder(serviceProvider)
                        .UseSockets(sockets =>
                        {
                            // Echo server
                            sockets.ListenLocalhost(5000, builder => builder.UseConnectionHandler<EchoServerApplication>());

                            // HTTP/1.1 server
                            sockets.Listen(IPAddress.Loopback, 5001, builder => builder.UseHttpServer(new HttpApplication()));

                            // SignalR Hub
                            sockets.Listen(IPAddress.Loopback, 5002, builder => builder.UseHub<Chat>());

                            // MQTT application
                            sockets.Listen(IPAddress.Loopback, 5003, builder => builder.UseConnectionHandler<MqttApplication>());
                        })
                        .Build();

            await server.StartAsync();

            Console.WriteLine("Process any key to exit");
            Console.ReadLine();

            await server.StopAsync();
        }
    }
}
