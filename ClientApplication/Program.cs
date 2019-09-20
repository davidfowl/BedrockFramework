using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using BedrockTransports;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClientApplication
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder()
                           .ConfigureServices(services =>
                           {
                               services.AddHostedService<ClientApplication>();
                           })
                           .Build();

            await host.RunAsync();
        }
    }

    public class ClientApplication : BackgroundService
    {
        private readonly ILoggerFactory _loggerFactory;

        public ClientApplication(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var clientFactory = new WebSocketConnectionFactory(_loggerFactory);
            var clientEndPoint = new UriEndPoint(new Uri("https://localhost:5003"));

            var connection = await clientFactory.ConnectAsync(clientEndPoint);
            Console.WriteLine($"Connected to {clientEndPoint}");

            Console.WriteLine("Echo server running, type into the console");

            _ = Console.OpenStandardInput().CopyToAsync(connection.Transport.Output, stoppingToken);
            await connection.Transport.Input.CopyToAsync(Console.OpenStandardOutput(), stoppingToken);
        }

    }
}
