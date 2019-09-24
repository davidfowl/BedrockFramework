using System;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bedrock.Framework;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using MQTTnet.Client.Subscribing;

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

    public class ClientApplication : BackgroundService, IMqttApplicationMessageReceivedHandler
    {
        private readonly ILoggerFactory _loggerFactory;

        public ClientApplication(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public Task HandleApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs eventArgs)
        {
            return Task.CompletedTask;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //var options = new MqttClientOptionsBuilder()
            //            .WithClientId("Client1")
            //            .WithTcpServer("127.0.0.1", 5008)
            //            .Build();

            //var factory = new MqttFactory();
            //var client = factory.CreateMqttClient();
            //await client.ConnectAsync(options, stoppingToken);
            //var subs = new MqttClientSubscribeOptions();
            //subs.TopicFilters.Add(new TopicFilter { Topic = "A" });
            //await client.SubscribeAsync(subs, stoppingToken);
            //await client.PublishAsync(new MqttApplicationMessage() { Topic = "A", Payload = Encoding.UTF8.GetBytes("Hello World") }, stoppingToken);
            //client.ApplicationMessageReceivedHandler = this;

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
