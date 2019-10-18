using System;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Bedrock.Framework;
using Bedrock.Framework.Protocols;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClientApplication
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var serviceProvider = new ServiceCollection().AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddConsole();
            })
            .BuildServiceProvider();

            await EchoServer(serviceProvider);
            // await HttpClient(serviceProvider);
            // await SignalR(serviceProvider);
        }

        private static async Task EchoServer(ServiceProvider serviceProvider)
        {
            var client = new ClientBuilder(serviceProvider)
                                    .UseSockets()
                                    .UseConnectionLogging()
                                    .Build();

            var connection = await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 5000));
            Console.WriteLine($"Connected to {connection.LocalEndPoint}");

            Console.WriteLine("Echo server running, type into the console");
            var reads = Console.OpenStandardInput().CopyToAsync(connection.Transport.Output);
            var writes = connection.Transport.Input.CopyToAsync(Stream.Null);

            await reads;
            await writes;
        }

        private static async Task HttpClient(ServiceProvider serviceProvider)
        {
            // Build the client pipeline
            var client = new ClientBuilder(serviceProvider)
                        .UseSockets()
                        .UseConnectionLogging()
                        .Build();

            await using var connection = await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 5001));

            // Use the HTTP/1.1 protocol
            var httpProtocol = HttpClientProtocol.CreateFromConnection(connection);

            while (true)
            {
                Console.Write("path> ");
                var path = Console.ReadLine();

                // Send a request (we're ignoring the response for now since it will be dumped to the console)
                await httpProtocol.SendAsync(new HttpRequestMessage(HttpMethod.Get, path));
            }
            // await response.Content.CopyToAsync(Console.OpenStandardOutput());
        }

        private static async Task SignalR(ServiceProvider serviceProvider)
        {
            var client = new ClientBuilder(serviceProvider)
                        .UseSockets()
                        .UseConnectionLogging()
                        .Build();

            var json = new JsonHubProtocol();
            var hubConnection = new HubConnection(client, 
                json, 
                new IPEndPoint(IPAddress.Loopback, 5002), 
                serviceProvider, 
                serviceProvider.GetRequiredService<ILoggerFactory>());

            hubConnection.On<string>("Send", data =>
            {
                // The connection logging will dump the raw payload on the wire
            });

            await hubConnection.StartAsync();

            while (true)
            {
                var line = Console.ReadLine();
                await hubConnection.InvokeAsync("Send", line);
            }
        }
    }
}
