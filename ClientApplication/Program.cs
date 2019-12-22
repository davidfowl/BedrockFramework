using System;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Bedrock.Framework;
using Bedrock.Framework.Protocols;
using Bedrock.Framework.Transports.Memory;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Protocols;

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
            Console.WriteLine("6. Length prefixed custom binary protocol");

            var keyInfo = Console.ReadKey();
            var targetIP = IPAddress.Parse("13.87.157.140");

            if (keyInfo.Key == ConsoleKey.D1)
            {
                Console.WriteLine("Running echo server example");
                await EchoServer(targetIP, serviceProvider);
            }
            else if (keyInfo.Key == ConsoleKey.D2)
            {
                Console.WriteLine("Running http client example");
                await HttpClient(targetIP, serviceProvider);
            }
            else if (keyInfo.Key == ConsoleKey.D3)
            {
                Console.WriteLine("Running SignalR example");
                await SignalR(targetIP);
            }
            else if (keyInfo.Key == ConsoleKey.D4)
            {
                Console.WriteLine("Running echo server with TLS example");
                await EchoServerWithTls(targetIP, serviceProvider);
            }
            else if (keyInfo.Key == ConsoleKey.D5)
            {
                Console.WriteLine("In Memory Transport Echo Server and client.");
                await InMemoryEchoTransport(targetIP, serviceProvider);
            }
            else if (keyInfo.Key == ConsoleKey.D6)
            {
                Console.WriteLine("Custom length prefixed protocol.");
                await CustomProtocol(targetIP, serviceProvider);
            }
        }

        private static async Task EchoServer(IPAddress address, IServiceProvider serviceProvider)
        {
            var client = new ClientBuilder(serviceProvider)
                                    .UseSockets()
                                    .UseConnectionLogging()
                                    .Build();

            var connection = await client.ConnectAsync(new IPEndPoint(address, 5000));
            Console.WriteLine($"Connected to {connection.LocalEndPoint}");

            Console.WriteLine("Echo server running, type into the console");
            var reads = Console.OpenStandardInput().CopyToAsync(connection.Transport.Output);
            var writes = connection.Transport.Input.CopyToAsync(Stream.Null);

            await reads;
            await writes;
        }

        private static async Task HttpClient(IPAddress address, IServiceProvider serviceProvider)
        {
            // TODO: Missing scenarios
            // - HTTP/2 needs to set the ALPN parameters (hard)
            // - Proxy support needs to know if the connection is secure

            // Build the client pipeline
            var client = new ClientBuilder(serviceProvider)
                        .UseSockets()
                        .UseDnsCaching(TimeSpan.FromHours(1))
                        .UseConnectionLogging()
                        .Build();

            await using var connection = await client.ConnectAsync(new IPEndPoint(address, 5001));

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

        private static async Task SignalR(IPAddress address)
        {
            var hubConnection = new HubConnectionBuilder()
                                .WithClientBuilder(new IPEndPoint(address, 5002), builder =>
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


        private static async Task EchoServerWithTls(IPAddress address, ServiceProvider serviceProvider)
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

            var connection = await client.ConnectAsync(new IPEndPoint(address, 5004));
            Console.WriteLine($"Connected to {connection.LocalEndPoint}");

            Console.WriteLine("Echo server running, type into the console");
            var reads = Console.OpenStandardInput().CopyToAsync(connection.Transport.Output);
            var writes = connection.Transport.Input.CopyToAsync(Stream.Null);

            await reads;
            await writes;
        }

        private static async Task InMemoryEchoTransport(IPAddress address, IServiceProvider serviceProvider)
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

            var connection = await client.ConnectAsync(endpoint: null);
            Console.WriteLine($"Connected to {connection.LocalEndPoint}");

            Console.WriteLine("Echo server running, type into the console");
            var reads = Console.OpenStandardInput().CopyToAsync(connection.Transport.Output);
            var writes = connection.Transport.Input.CopyToAsync(Stream.Null);

            await reads;
            await writes;

            await server.StopAsync();
        }

        private static async Task CustomProtocol(IPAddress address, IServiceProvider serviceProvider)
        {
            var client = new ClientBuilder(serviceProvider)
                                    .UseSockets()
                                    .UseConnectionLogging()
                                    .Build();

            await using var connection = await client.ConnectAsync(new IPEndPoint(address, 5005));
            Console.WriteLine($"Connected to {connection.LocalEndPoint}");

            var protocol = new LengthPrefixedProtocol();
            var reader = Protocol.CreateReader(connection, protocol);
            var writer = Protocol.CreateWriter(connection, protocol);

            while (true)
            {
                var line = Console.ReadLine();
                await writer.WriteAsync(new Message(Encoding.UTF8.GetBytes(line)));
            }
        }

    }

    // Property bag needed on ConnectAsync and BindAsync
    // Maybe change EndPoint to some other binding abstraction
}
