using System;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace BedrockTransports
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            using var shutdownTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                shutdownTokenSource.Cancel();
            };

            var token = shutdownTokenSource.Token;

            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
            });

            // (var serverFactory, var clientFactory, var serverEndPoint, var clientEndPoint) = GetHttp2Transport(loggerFactory);
            (var serverFactory, var clientFactory, var serverEndPoint, var clientEndPoint) = GetWebSocketTransport(loggerFactory);
            // (var serverFactory, var clientFactory, var serverEndPoint, var clientEndPoint) = GetAzureSignalRTransport(loggerFactory);
            // (var serverFactory, var clientFactory, var serverEndPoint, var clientEndPoint) = GetNamedPipesTransport(loggerFactory);

            // Connect to the server endpoint
            var listener = await serverFactory.BindAsync(serverEndPoint);
            Console.WriteLine($"Listening on {serverEndPoint}");

            // Open a client connection to the listener
            var connection = await clientFactory.ConnectAsync(clientEndPoint);
            Console.WriteLine($"Connected to {clientEndPoint}");

            var serverTask = RunEchoServerAsync(listener, token);
            var clientTask = RunClientAsync(connection, token);

            Console.WriteLine("Echo server running, type into the console");

            var task = await Task.WhenAny(clientTask, serverTask);

            if (task == clientTask)
            {
                Console.WriteLine("Client ended");
            }
        }

        private static (IConnectionListenerFactory, IConnectionFactory, EndPoint, EndPoint) GetAzureSignalRTransport(ILoggerFactory loggerFactory)
        {
            // This is a transport based on the AzureSignalR protocol, it gives you a full duplex mutliplexed connection over the 
            // the internet

            // Put your azure SignalR connection string here (securely of course!)
            var connectionString = "";

            var serverFactory = new AzureSignalRConnectionListenerFactory(loggerFactory);
            var clientFactory = new AzureSignalRConnectionFactory(loggerFactory);
            var serverEndPoint = new AzureSignalREndPoint(connectionString, "myhub", AzureSignalREndpointType.Server);
            var clientEndPoint = new AzureSignalREndPoint(connectionString, "myhub", AzureSignalREndpointType.Client);

            return (serverFactory, clientFactory, serverEndPoint, clientEndPoint);
        }

        private static (IConnectionListenerFactory, IConnectionFactory, EndPoint, EndPoint) GetWebSocketTransport(ILoggerFactory loggerFactory)
        {
            // This is an websockets transport based on Kestrel and ClientWebSocket
            var serverFactory = new WebSocketConnectionListenerFactory(loggerFactory);
            var clientFactory = new WebSocketConnectionFactory(loggerFactory);
            var endPoint = new UriEndPoint(new Uri("https://localhost:5004"));

            return (serverFactory, clientFactory, endPoint, endPoint);
        }

        private static (IConnectionListenerFactory, IConnectionFactory, EndPoint, EndPoint) GetHttp2Transport(ILoggerFactory loggerFactory)
        {
            // This is an http/2 transport based on kestrel and httpclient, each connection is mapped to an HTTP/2 stream
            var serverFactory = new Http2ConnectionListenerFactory(loggerFactory);
            var clientFactory = new Http2ConnectionFactory();
            var endPoint = new UriEndPoint(new Uri("https://localhost:5003"));

            return (serverFactory, clientFactory, endPoint, endPoint);
        }

        private static (IConnectionListenerFactory, IConnectionFactory, EndPoint, EndPoint) GetNamedPipesTransport(ILoggerFactory loggerFactory)
        {
            // This is a transport using named pipes
            var serverFactory = new NamedPipeConnectionListenerFactory();
            var clientFactory = new NamedPipeConnectionFactory();
            var endPoint = new NamedPipeEndPoint("mypipe");

            return (serverFactory, clientFactory, endPoint, endPoint);
        }

        public static async Task RunClientAsync(ConnectionContext connection, CancellationToken cancellationToken = default)
        {
            _ = Console.OpenStandardInput().CopyToAsync(connection.Transport.Output, cancellationToken);
            await connection.Transport.Input.CopyToAsync(Console.OpenStandardOutput(), cancellationToken);
        }

        public static async Task RunEchoServerAsync(IConnectionListener listener, CancellationToken cancellationToken = default)
        {
            while (true)
            {
                var connection = await listener.AcceptAsync(cancellationToken);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        // This is the simplest implementation of an echo server, copy the input to the output
                        await connection.Transport.Input.CopyToAsync(connection.Transport.Output, cancellationToken);
                    }
                    finally
                    {
                        await connection.DisposeAsync();
                    }
                });
            }

        }
    }
}
