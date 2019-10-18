using System;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Bedrock.Framework;
using Bedrock.Framework.Protocols;
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
        }

        private static async Task EchoServer(ServiceProvider serviceProvider)
        {
            var client = new ClientBuilder(serviceProvider)
                                    .UseSockets()
                                    .UseConnectionLogging()
                                    .Build();

            var connection = await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 5010));
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

            await using var connection = await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 5007));

            // Use the HTTP/1.1 protocol
            var httpProtocol = HttpClientProtocol.CreateFromConnection(connection);

            // Send a request
            var response = await httpProtocol.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/"));

            // await response.Content.CopyToAsync(Console.OpenStandardOutput());
        }
    }
}
