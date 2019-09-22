using System;
using System.Net;
using System.Threading.Tasks;
using Bedrock.Framework;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ServerApplication
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder()
                           .ConfigureServices((context, services) =>
                           {
                               // Frameworks
                               services.AddSignalRCore();

                               // Configure the server
                               services.ConfigureServer(options =>
                               {
                                   options.ListenWebSocket(
                                       new Uri("https://localhost:5003"),
                                       builder => builder.UseConnectionHandler<EchoServerApplication>());

                                   options.ListenHttp2(
                                       new Uri("https://localhost:5004"),
                                       builder => builder.UseConnectionHandler<EchoServerApplication>());

                                   options.ListenSocket(
                                       new IPEndPoint(IPAddress.Loopback, 5005),
                                       builder => builder.UseConnectionHandler<EchoServerApplication>());

                                   // This is a transport based on the AzureSignalR protocol, it gives you a full duplex mutliplexed connection over the 
                                   // the internet
                                   // Put your azure SignalR connection string in configuration

                                   //var connectionString = context.Configuration["AzureSignalR:ConnectionString"];
                                   //options.ListenAzureSignalR(connectionString, "myhub",
                                   //    builder => builder.UseConnectionHandler<EchoServerApplication>());

                                   // SignalR on TCP
                                   options.Listen(IPAddress.Loopback, 5006, builder => builder.UseHub<Chat>());
                               });
                           })
                           .Build();

            await host.RunAsync();
        }
    }
}
