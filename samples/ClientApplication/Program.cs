using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Bedrock.Framework;
using Bedrock.Framework.Experimental.Protocols.RabbitMQ;
using Bedrock.Framework.Experimental.Protocols.Memcached;
using Bedrock.Framework.Protocols;
using Bedrock.Framework.Transports.Memory;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Protocols;
using Bedrock.Framework.Experimental.Protocols.RabbitMQ.Methods;
using ServerApplication.Framing.VariableSized.LengthFielded;
using Bedrock.Framework.Experimental.Protocols.Framing.VariableSized.LengthFielded;

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
            Console.WriteLine("7. Header prefixed protocol");
            Console.WriteLine("8. Talk to local docker daemon");
            Console.WriteLine("9. Memcached protocol");
            Console.WriteLine("0. RabbitMQ protocol");

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
                    await CustomProtocol();
                }
                else if (keyInfo.Key == ConsoleKey.D7)
                {
                    Console.WriteLine("Variable size length fielded protocol.");
                    await VariableSizedLengthFieldedProtocol();
                }
                else if (keyInfo.Key == ConsoleKey.D8)
                {
                    Console.WriteLine("Talk to local docker daemon");
                    await DockerDaemon(serviceProvider);
                }
                else if (keyInfo.Key == ConsoleKey.D9)
                {
                    Console.WriteLine("Send Request To Memcached");
                    await MemcachedProtocol(serviceProvider);
                }
                else if (keyInfo.Key == ConsoleKey.D0)
                {
                    Console.WriteLine("RabbitMQ test");
                    await RabbitMQProtocol(serviceProvider);
                }
            }
        }

        private static async Task RabbitMQProtocol(IServiceProvider serviceProvider)
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Error);
                builder.AddConsole();
            });

            var client = new ClientBuilder(serviceProvider)
                .UseSockets()
                .UseConnectionLogging(loggerFactory: loggerFactory)
                .Build();

            var ipAddress = IPAddress.Parse("127.0.0.1");
            var connection = await client.ConnectAsync(new IPEndPoint(ipAddress, 5672));
            var rabbitMqClientProtocol = new RabbitMQClientProtocol(connection);

            await rabbitMqClientProtocol.SendAsync(new RabbitMQProtocolVersionHeader());
            var connectionStart = await rabbitMqClientProtocol.ReceiveAsync<ConnectionStart>();
            //           
            byte[] credentials = Encoding.UTF8.GetBytes("\0guest" + "\0guest");

            await rabbitMqClientProtocol.SendAsync(new ConnectionOk(connectionStart.SecurityMechanims, new ReadOnlyMemory<byte>(credentials), connectionStart.Locale));
            var connectionTune = await rabbitMqClientProtocol.ReceiveAsync<ConnectionTune>();

            await rabbitMqClientProtocol.SendAsync(new ConnectionTuneOk(connectionTune.MaxChannel, connectionTune.MaxFrame, connectionTune.HeartBeat));
            await rabbitMqClientProtocol.SendAsync(new ConnectionOpen(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("/")),
                new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(string.Empty)),
                0));
            var connectionOpenOk = await rabbitMqClientProtocol.ReceiveAsync<ConnectionOpenOk>();

            ushort channelId = 1;
            await rabbitMqClientProtocol.SendAsync(new ChannelOpen(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(string.Empty)), channelId));
            var channelOpenOk = await rabbitMqClientProtocol.ReceiveAsync<ChannelOpenOk>();

            await rabbitMqClientProtocol.SendAsync(new QueueDeclare(channelId, 0, "queue_test"));
            var queueDeclareOk = await rabbitMqClientProtocol.ReceiveAsync<QueueDeclareOk>();

            await rabbitMqClientProtocol.SendAsync(new QueueDelete(channelId, 0, "queue_test"));
            var queueDeleteOk = await rabbitMqClientProtocol.ReceiveAsync<QueueDeleteOk>();
        }

        private static async Task MemcachedProtocol(IServiceProvider serviceProvider)
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Error);
                builder.AddConsole();
            });

            var client = new ClientBuilder(serviceProvider)
                .UseSockets()
                .UseConnectionLogging(loggerFactory: loggerFactory)
                .Build();

            var ipAddress = IPAddress.Parse("127.0.0.1");
            var connection = await client.ConnectAsync(new IPEndPoint(ipAddress, 11211));
            MemcachedProtocol memcachedProtocol = new MemcachedProtocol(connection);

            await memcachedProtocol.Set("Hello", Encoding.UTF8.GetBytes("World"), TimeSpan.FromMinutes(30));
            var checkSet = await memcachedProtocol.Get("Hello");
            Console.WriteLine($"checkSet result :{Encoding.UTF8.GetString(checkSet)}");

            await memcachedProtocol.Replace("Hello", Encoding.UTF8.GetBytes("World replaced"), TimeSpan.FromMinutes(30));
            var checkReplace = await memcachedProtocol.Get("Hello");
            Console.WriteLine($"checkReplace result :{Encoding.UTF8.GetString(checkReplace)}");
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

        private static async Task CustomProtocol()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddConsole();
            });

            var client = new ClientBuilder()
                                    .UseSockets()
                                    .UseConnectionLogging(loggerFactory: loggerFactory)
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

        private static async Task VariableSizedLengthFieldedProtocol()
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddConsole();
            });

            var client = new ClientBuilder()
                .UseSockets()
                .UseConnectionLogging(loggerFactory: loggerFactory)
                .Build();

            await using var connection = await client.ConnectAsync(new IPEndPoint(IPAddress.Loopback, 5006));
            Console.WriteLine($"Connected to {connection.RemoteEndPoint}");
            Console.WriteLine("Enter 'c' to close the connection.");

            var headerFactory = new HeaderFactory();

            var protocol = new LengthFieldedProtocol(Helper.HeaderLength, (headerSequence) => headerFactory.CreateHeader(headerSequence));
            await using var writer = connection.CreateWriter();

            while (true)
            {
                Console.WriteLine("Enter the text: ");
                var line = Console.ReadLine();
                if (line.Equals("c"))
                {
                    break;
                }

                Console.WriteLine("Enter a number as custom data: ");
                int someCustomData = int.Parse(Console.ReadLine());

                var payload = Encoding.UTF8.GetBytes(line);
                var header = headerFactory.CreateHeader(payload.Length, someCustomData);
                var frame = new Frame(header, payload);

                await writer.WriteAsync(protocol, frame);
            }

            connection.Abort();
            await connection.DisposeAsync();
        }

        private static async Task DockerDaemon(IServiceProvider serviceProvider)
        {
            var client = new ClientBuilder(serviceProvider)
                        .UseConnectionFactory(new NamedPipeConnectionFactory())
                        .UseConnectionLogging()
                        .Build();

            await using var connection = await client.ConnectAsync(new NamedPipeEndPoint("docker_engine"));

            // Use the HTTP/1.1 protocol
            var httpProtocol = new HttpClientProtocol(connection);

            while (true)
            {
                // Console.Write("http1.1> ");
                var path = Console.ReadLine();

                if (path == null)
                {
                    break;
                }

                // Console.WriteLine();

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
    }

    // Property bag needed on ConnectAsync and BindAsync
    // Maybe change EndPoint to some other binding abstraction
}
