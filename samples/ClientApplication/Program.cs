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
//using Bedrock.Framework.Experimental.Protocols.RabbitMQ;
//using Bedrock.Framework.Experimental.Protocols.Memcached;
using Bedrock.Framework.Protocols;
using Bedrock.Framework.Transports.Memory;
using Microsoft.AspNetCore.Connections;
//using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Protocols;

using NamedPipeEndPoint = Bedrock.Framework.NamedPipeEndPoint;
//using Bedrock.Framework.Experimental.Protocols.RabbitMQ.Methods;

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
            Console.WriteLine("2. (Disabled) HttpClient");
            Console.WriteLine("3. SignalR");
            Console.WriteLine("4. (Disabled) Echo Server With TLS enabled");
            Console.WriteLine("5. In Memory Transport Echo Server and client");
            Console.WriteLine("6. Length prefixed custom binary protocol");
            Console.WriteLine("7. (Disabled) Talk to local docker dameon");
            Console.WriteLine("8. (Disabled) Memcached protocol");
            Console.WriteLine("9. (Disabled) RebbitMQ protocol");

            while (true)
            {
                var keyInfo = Console.ReadKey();

                if (keyInfo.Key == ConsoleKey.D1)
                {
                    Console.WriteLine("Running echo server example");
                    await EchoServer(serviceProvider);
                }
                //else if (keyInfo.Key == ConsoleKey.D2)
                //{
                //    Console.WriteLine("Running http client example");
                //    await HttpClient(serviceProvider);
                //}
                else if (keyInfo.Key == ConsoleKey.D3)
                {
                    Console.WriteLine("Running SignalR example");
                    //await SignalR();
                }
                else if (keyInfo.Key == ConsoleKey.D5)
                {
                    Console.WriteLine("In Memory Transport Echo Server and client.");
                    await InMemoryEchoTransport(serviceProvider);
                }
                else if (keyInfo.Key == ConsoleKey.D6)
                {
                    Console.WriteLine("Custom length prefixed protocol.");
                    //await CustomProtocol();
                }
                //else if (keyInfo.Key == ConsoleKey.D7)
                //{
                //    Console.WriteLine("Talk to local docker daemon");
                //    await DockerDaemon(serviceProvider);
                //}
                //else if (keyInfo.Key == ConsoleKey.D8)
                //{
                //    Console.WriteLine("Send Request To Memcached");
                //    await MemcachedProtocol(serviceProvider);
                //}
                //else if (keyInfo.Key == ConsoleKey.D9)
                //{
                //    Console.WriteLine("RabbitMQ test");
                //    await RabbitMQProtocol(serviceProvider);
                //}
            }
        }

        //private static async Task RabbitMQProtocol(IServiceProvider serviceProvider)
        //{
        //    var loggerFactory = LoggerFactory.Create(builder =>
        //    {
        //        builder.SetMinimumLevel(LogLevel.Error);
        //        builder.AddConsole();
        //    });

        //    var client = new ClientBuilder(serviceProvider)
        //        .UseNamedPipes()
        //        .UseConnectionLogging(loggerFactory: loggerFactory)
        //        .Build();

        //    var pipeEndpoint = new NamedPipeEndPoint("default_mqtt");
        //    var connection = await client.ConnectAsync(pipeEndpoint);
        //    var rabbitMqClientProtocol = new RabbitMQClientProtocol(connection);

        //    await rabbitMqClientProtocol.SendAsync(new RabbitMQProtocolVersionHeader());
        //    var connectionStart = await rabbitMqClientProtocol.ReceiveAsync<ConnectionStart>();
        //    //           
        //    byte[] credentials = Encoding.UTF8.GetBytes("\0guest" + "\0guest");

        //    await rabbitMqClientProtocol.SendAsync(new ConnectionOk(connectionStart.SecurityMechanims, new ReadOnlyMemory<byte>(credentials), connectionStart.Locale));
        //    var connectionTune = await rabbitMqClientProtocol.ReceiveAsync<ConnectionTune>();

        //    await rabbitMqClientProtocol.SendAsync(new ConnectionTuneOk(connectionTune.MaxChannel, connectionTune.MaxFrame, connectionTune.HeartBeat));
        //    await rabbitMqClientProtocol.SendAsync(new ConnectionOpen(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("/")),
        //        new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(string.Empty)),
        //        0));
        //    var connectionOpenOk = await rabbitMqClientProtocol.ReceiveAsync<ConnectionOpenOk>();

        //    ushort channelId = 1;
        //    await rabbitMqClientProtocol.SendAsync(new ChannelOpen(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(string.Empty)), channelId));
        //    var channelOpenOk = await rabbitMqClientProtocol.ReceiveAsync<ChannelOpenOk>();

        //    await rabbitMqClientProtocol.SendAsync(new QueueDeclare(channelId, 0, "queue_test"));
        //    var queueDeclareOk = await rabbitMqClientProtocol.ReceiveAsync<QueueDeclareOk>();

        //    await rabbitMqClientProtocol.SendAsync(new QueueDelete(channelId, 0, "queue_test"));
        //    var queueDeleteOk = await rabbitMqClientProtocol.ReceiveAsync<QueueDeleteOk>();
        //}

        //private static async Task MemcachedProtocol(IServiceProvider serviceProvider)
        //{
        //    var loggerFactory = LoggerFactory.Create(builder =>
        //    {
        //        builder.SetMinimumLevel(LogLevel.Error);
        //        builder.AddConsole();
        //    });

        //    var client = new ClientBuilder(serviceProvider)
        //        .UseNamedPipes()
        //        .UseConnectionLogging(loggerFactory: loggerFactory)
        //        .Build();

        //    var pipeEndpoint = new NamedPipeEndPoint("default_memcache");
        //    var connection = await client.ConnectAsync(pipeEndpoint);
        //    MemcachedProtocol memcachedProtocol = new MemcachedProtocol(connection);

        //    await memcachedProtocol.Set("Hello", Encoding.UTF8.GetBytes("World"), TimeSpan.FromMinutes(30));
        //    var checkSet = await memcachedProtocol.Get("Hello");
        //    Console.WriteLine($"checkSet result :{Encoding.UTF8.GetString(checkSet)}");

        //    await memcachedProtocol.Replace("Hello", Encoding.UTF8.GetBytes("World replaced"), TimeSpan.FromMinutes(30));
        //    var checkReplace = await memcachedProtocol.Get("Hello");
        //    Console.WriteLine($"checkReplace result :{Encoding.UTF8.GetString(checkReplace)}");
        //}

        private static async Task EchoServer(IServiceProvider serviceProvider)
        {
            var client = new ClientBuilder(serviceProvider)
                                    .UseNamedPipes()
                                    .UseConnectionLogging()
                                    .Build();

            var connection = await client.ConnectAsync(new NamedPipeEndPoint("default_echo"));
            Console.WriteLine($"Connected to {connection.LocalEndPoint}");

            Console.WriteLine("Echo server running, type into the console");
            var reads = Console.OpenStandardInput().CopyToAsync(connection.Transport.Output);
            var writes = connection.Transport.Input.CopyToAsync(Stream.Null);

            await reads;
            await writes;
        }

        //private static async Task HttpClient(IServiceProvider serviceProvider)
        //{
        //    // TODO: Missing scenarios
        //    // - HTTP/2 needs to set the ALPN parameters (hard)
        //    // - Proxy support needs to know if the connection is secure

        //    // Build the client pipeline
        //    var client = new ClientBuilder(serviceProvider)
        //                .UseNamedPipes()
        //                .UseDnsCaching(TimeSpan.FromHours(1))
        //                .UseConnectionLogging()
        //                .Build();

        //    await using var connection = await client.ConnectAsync(new NamedPipeEndPoint("default_http11"));

        //    // Use the HTTP/1.1 protocol
        //    var httpProtocol = new HttpClientProtocol(connection);

        //    while (true)
        //    {
        //        Console.Write("http1.1> ");
        //        var path = Console.ReadLine();

        //        if (path == null)
        //        {
        //            break;
        //        }

        //        if (path == string.Empty)
        //        {
        //            path = "/";
        //        }

        //        var request = new HttpRequestMessage(HttpMethod.Get, path);
        //        request.Headers.Host = "localhost";

        //        var response = await httpProtocol.SendAsync(request);

        //        await response.Content.CopyToAsync(Console.OpenStandardOutput());

        //        Console.WriteLine();
        //    }
        //}

        //private static async Task SignalR()
        //{
        //    var hubConnection = new HubConnectionBuilder()
        //                        .WithClientBuilder(new NamedPipeEndPoint("default_signalr"), builder =>
        //                        {
        //                            builder.UseNamedPipes()
        //                                   .UseConnectionLogging();
        //                        })
        //                        .ConfigureLogging(builder =>
        //                        {
        //                            builder.SetMinimumLevel(LogLevel.Debug);
        //                            builder.AddConsole();
        //                        })
        //                        .WithAutomaticReconnect()
        //                        .Build();

        //    hubConnection.On<string>("Send", data =>
        //    {
        //        // The connection logging will dump the raw payload on the wire
        //    });

        //    await hubConnection.StartAsync();

        //    while (true)
        //    {
        //        var line = Console.ReadLine();
        //        await hubConnection.InvokeAsync("Send", line);
        //    }
        //}


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
                            _ = builder.UseConnectionLogging("Server").Run(c => c.Transport.Input.CopyToAsync(c.Transport.Output));
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

        //private static async Task CustomProtocol()
        //{
        //    var loggerFactory = LoggerFactory.Create(builder =>
        //    {
        //        builder.SetMinimumLevel(LogLevel.Debug);
        //        builder.AddConsole();
        //    });

        //    var client = new ClientBuilder()
        //                            .UseNamedPipes()
        //                            .UseConnectionLogging(loggerFactory: loggerFactory)
        //                            .Build();

        //    await using var connection = await client.ConnectAsync(new NamedPipeEndPoint("default"));
        //    Console.WriteLine($"Connected to {connection.LocalEndPoint}");

        //    var protocol = new LengthPrefixedProtocol();
        //    var reader = connection.CreateReader();
        //    var writer = connection.CreateWriter();

        //    while (true)
        //    {
        //        var line = Console.ReadLine();
        //        await writer.WriteAsync(protocol, new Message(Encoding.UTF8.GetBytes(line)));
        //        var result = await reader.ReadAsync(protocol);

        //        if (result.IsCompleted)
        //        {
        //            break;
        //        }

        //        reader.Advance();
        //    }
        //}

        //private static async Task DockerDaemon(IServiceProvider serviceProvider)
        //{
        //    var client = new ClientBuilder(serviceProvider)
        //                .UseConnectionFactory(new NamedPipeConnectionFactory())
        //                .UseConnectionLogging()
        //                .Build();

        //    await using var connection = await client.ConnectAsync(new NamedPipeEndPoint("docker_engine"));

        //    // Use the HTTP/1.1 protocol
        //    var httpProtocol = new HttpClientProtocol(connection);

        //    while (true)
        //    {
        //        // Console.Write("http1.1> ");
        //        var path = Console.ReadLine();

        //        if (path == null)
        //        {
        //            break;
        //        }

        //        // Console.WriteLine();

        //        if (path == string.Empty)
        //        {
        //            path = "/";
        //        }

        //        var request = new HttpRequestMessage(HttpMethod.Get, path);
        //        request.Headers.Host = "localhost";

        //        var response = await httpProtocol.SendAsync(request);

        //        await response.Content.CopyToAsync(Console.OpenStandardOutput());

        //        Console.WriteLine();
        //    }
        //}
    }

    // Property bag needed on ConnectAsync and BindAsync
    // Maybe change EndPoint to some other binding abstraction
}
