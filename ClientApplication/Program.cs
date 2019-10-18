using System;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Threading.Tasks;
using Bedrock.Framework;
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
            // await writes;
        }
    }
}
