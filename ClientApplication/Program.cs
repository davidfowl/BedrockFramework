using System;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Bedrock.Framework;
using Bedrock.Framework.Protocols;
using Bedrock.Framework.Transports.Memory;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR.Client;
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
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddConsole();
            })
            .BuildServiceProvider();

            Console.WriteLine("Samples: ");
            Console.WriteLine("1. Echo Server");
            Console.WriteLine("2. HttpClient");
            Console.WriteLine("3. SignalR");
            Console.WriteLine("4. Echo Server With TLS enabled");
            Console.WriteLine("5. In Memory Transport Echo Server and client");

            var keyInfo = Console.ReadKey();

            if (keyInfo.Key == ConsoleKey.D1)
            {
                Console.WriteLine("Running echo server example");
                await EchoServer(serviceProvider);
            }
            else if (keyInfo.Key == ConsoleKey.D2)
            {
                Console.WriteLine("Running http client example");
                await HttpClient(serviceProvider);
            }
            else if (keyInfo.Key == ConsoleKey.D3)
            {
                Console.WriteLine("Running SignalR example");
                await SignalR();
            }
            else if (keyInfo.Key == ConsoleKey.D4)
            {
                Console.WriteLine("Running echo server with TLS example");
                await EchoServerWithTls(serviceProvider);
            }
            else if (keyInfo.Key == ConsoleKey.D5)
            {
                Console.WriteLine("In Memory Transport Echo Server and client.");
                await InMemoryEchoTransport(serviceProvider);
            }
        }

        private static async Task EchoServer(IServiceProvider serviceProvider)
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

        private static async Task HttpClient(IServiceProvider serviceProvider)
        {
            // Build the client pipeline
            var client = new ClientBuilder(serviceProvider)
                        .UseSockets()
                        .UseDnsCaching(TimeSpan.FromHours(1))
                        .UseConnectionLogging()
                        .Build();

            await using var connection = await client.ConnectAsync(new DnsEndPoint("localhost", 5001));

            // Use the HTTP/1.1 protocol
            var httpProtocol = HttpClientProtocol.CreateFromConnection(connection);

            while (true)
            {
                Console.Write("http1.1> ");
                var path = Console.ReadLine();

                // Send a request (we're ignoring the response for now since it will be dumped to the console)
                await httpProtocol.SendAsync(new HttpRequestMessage(HttpMethod.Get, path));
            }
        }

        private static async Task SignalR()
        {
            var hubConnection = new HubConnectionBuilder()
                                .WithClientBuilder(new IPEndPoint(IPAddress.Loopback, 5002), builder =>
                                {
                                    builder.UseSockets()
                                           .UseConnectionLogging();
                                })
                                .ConfigureLogging(builder =>
                                {
                                    builder.SetMinimumLevel(LogLevel.Debug);
                                    builder.AddConsole();
                                })
                                .WithAutomaticReconnect()
                                .Build();

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


        private static async Task EchoServerWithTls(ServiceProvider serviceProvider)
        {
            var client = new ClientBuilder(serviceProvider)
                                    .UseSockets()
                                    .UseConnectionLogging()
                                    .UseClientTls(options =>
                                    {
                                        options.OnAuthenticateAsClient = (connection, o) =>
                                        {
                                            o.TargetHost = "foo";
                                        };

                                        options.LocalCertificate = new X509Certificate2("testcert.pfx", "testcert");

                                        // NOTE: Do not do this in a production environment
                                        options.AllowAnyRemoteCertificate();
                                    })
                                    .Build();

            var connection = await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 5004));
            Console.WriteLine($"Connected to {connection.LocalEndPoint}");

            Console.WriteLine("Echo server running, type into the console");
            var reads = Console.OpenStandardInput().CopyToAsync(connection.Transport.Output);
            var writes = connection.Transport.Input.CopyToAsync(Stream.Null);

            await reads;
            await writes;
        }

        private static async Task InMemoryEchoTransport(IServiceProvider serviceProvider)
        {
            var memoryTransport = new MemoryTransport();

            var client = new ClientBuilder(serviceProvider)
                                    .UseConnectionFactory(memoryTransport)
                                    .UseConnectionLogging("Client")
                                    .Build();

            var server = new ServerBuilder(serviceProvider)
                        .Listen(endPoint: null, memoryTransport, builder =>
                        {
                            builder.UseConnectionLogging("Server").Run(connection => connection.Transport.Input.CopyToAsync(connection.Transport.Output));
                        })
                        .Build();

            await server.StartAsync();
            Console.WriteLine("Started Server");

            var connection = await client.ConnectAsync(endPoint: null);
            Console.WriteLine($"Connected to {connection.LocalEndPoint}");

            Console.WriteLine("Echo server running, type into the console");
            var reads = Console.OpenStandardInput().CopyToAsync(connection.Transport.Output);
            var writes = connection.Transport.Input.CopyToAsync(Stream.Null);

            await reads;
            await writes;

            await server.StopAsync();

        }
    }
}
