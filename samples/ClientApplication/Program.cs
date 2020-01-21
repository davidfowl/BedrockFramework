using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bedrock.Framework;
using Bedrock.Framework.Experimental.Protocols.Kafka;
using Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Requests;
using Bedrock.Framework.Experimental.Protocols.Kafka.Messages.Responses;
using Bedrock.Framework.Experimental.Protocols.Kafka.Models;
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
            var serviceProvider = new ServiceCollection()
                .AddKafkaProtocol()
                .AddLogging(builder =>
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
            Console.WriteLine("7. Kafka Consumer");

            await KafkaConsumer(serviceProvider);

            while (true)
            {
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
                else if (keyInfo.Key == ConsoleKey.D6)
                {
                    Console.WriteLine("Custom length prefixed protocol.");
                    await CustomProtocol(serviceProvider);
                }
                else if (keyInfo.Key == ConsoleKey.D7)
                {
                    Console.WriteLine("Kafka Consumer");
                    await KafkaConsumer(serviceProvider);
                }
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
            // TODO: Missing scenarios
            // - HTTP/2 needs to set the ALPN parameters (hard)
            // - Proxy support needs to know if the connection is secure

            // Build the client pipeline
            var client = new ClientBuilder(serviceProvider)
                        .UseSockets()
                        .UseDnsCaching(TimeSpan.FromHours(1))
                        .UseConnectionLogging()
                        .Build();

            await using var connection = await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 5001));

            // Use the HTTP/1.1 protocol
            var httpProtocol = new HttpClientProtocol(connection);

            while (true)
            {
                Console.Write("http1.1> ");
                var path = Console.ReadLine();

                if (path == null)
                {
                    break;
                }

                if (path == string.Empty)
                {
                    path = "/";
                }

                var request = new HttpRequestMessage(HttpMethod.Get, path);
                request.Headers.Host = "localhost";

                var response = await httpProtocol.SendAsync(request);

                await response.Content.CopyToAsync(Console.OpenStandardOutput());

                Console.WriteLine();
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

            var connection = await client.ConnectAsync(endpoint: null);
            Console.WriteLine($"Connected to {connection.LocalEndPoint}");

            Console.WriteLine("Echo server running, type into the console");
            var reads = Console.OpenStandardInput().CopyToAsync(connection.Transport.Output);
            var writes = connection.Transport.Input.CopyToAsync(Stream.Null);

            await reads;
            await writes;

            await server.StopAsync();
        }

        private static async Task KafkaConsumer(IServiceProvider serviceProvider)
        {
            var client = new ClientBuilder(serviceProvider)
                .UseSockets()
                //.UseConnectionLogging()
                .Build();

            var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (_, args) =>
            {
                cts.Cancel();
            };

            await using var connection = await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 9092), cts.Token);
            Console.WriteLine($"Connected to {connection.LocalEndPoint}");
            Console.WriteLine();

            Console.Write("What Topic should be consumed from?: ");
            var topic = "test";// Console.ReadLine();

            var clientId = "console-producer";

            using var kafkaProtocol = serviceProvider.GetRequiredService<KafkaProtocol>();

            // Can't find a way of getting ConnectionContexts injected...
            await kafkaProtocol.SetClientConnectionAsync(connection, clientId: clientId);

            var prompt = $"{clientId}:{topic}>";

            // Only support 1 topic and partition 0. So we can cache this.
            var topar = new TopicPartitions(topic, new Partition(0));

            while (!cts.IsCancellationRequested)
            {
                Console.Write(prompt);
                var message = Console.ReadLine();

                var bytes = Encoding.UTF8.GetBytes(message);

                var produceRequest = new ProduceRequestV0(
                    new ProducePayload(
                        ref topar,
                        key: null,
                        value: bytes));

                var produceResponse = await kafkaProtocol.SendAsync<ProduceRequestV0, ProduceResponseV0>(connection, produceRequest);
            }
        }

        private static async Task CustomProtocol(IServiceProvider serviceProvider)
        {
            var client = new ClientBuilder(serviceProvider)
                                    .UseSockets()
                                    .UseConnectionLogging()
                                    .Build();

            await using var connection = await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 5005));
            Console.WriteLine($"Connected to {connection.LocalEndPoint}");

            var protocol = new LengthPrefixedProtocol();
            var reader = connection.CreateReader();
            var writer = connection.CreateWriter();

            while (true)
            {
                var line = Console.ReadLine();
                await writer.WriteAsync(protocol, new Message(Encoding.UTF8.GetBytes(line)));
                var result = await reader.ReadAsync(protocol);

                if (result.IsCompleted)
                {
                    break;
                }

                reader.Advance();
            }
        }
    }

    // Property bag needed on ConnectAsync and BindAsync
    // Maybe change EndPoint to some other binding abstraction
}
