using System;
using System.IO.Pipelines;
using System.Net;
using System.Text;
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

            (var serverFactory, var clientFactory, var serverEndPoint, var clientEndPoint) = GetHttp2Transport(loggerFactory);
            // (var serverFactory, var clientFactory, var serverEndPoint, var clientEndPoint) = GetAzureSignalRTransport(loggerFactory);

            var listener = await serverFactory.BindAsync(serverEndPoint);

            Console.WriteLine($"Listening on {serverEndPoint}");

            var serverTask = RunEchoServerAsync(listener, token);
            var clientTask = RunClientAsync(clientFactory, clientEndPoint, token);

            try
            {
                await Task.WhenAll(clientTask, serverTask);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
            }
        }

        private static (IConnectionListenerFactory, IConnectionFactory, EndPoint serverEndpoint, EndPoint clientEndpoint) GetAzureSignalRTransport(ILoggerFactory loggerFactory)
        {
            // Put your azure SignalR connection string here (securely of course!)
            var connectionString = "";

            var serverFactory = new AzureSignalRConnectionListenerFactory(loggerFactory);
            var clientFactory = new AzureSignalRConnectionFactory(loggerFactory);
            var serverEndPoint = new AzureSignalREndPoint(connectionString, "myhub", AzureSignalREndpointType.Server);
            var clientEndPoint = new AzureSignalREndPoint(connectionString, "myhub", AzureSignalREndpointType.Client);

            return (serverFactory, clientFactory, serverEndPoint, clientEndPoint);
        }

        private static (IConnectionListenerFactory, IConnectionFactory, EndPoint serverEndpoint, EndPoint clientEndpoint) GetHttp2Transport(ILoggerFactory loggerFactory)
        {
            // This is an http/2 transport based on kestrel and httpclient, each connection is mapped to an HTTP/2 stream
            var serverFactory = new Http2ConnectionListenerFactory(loggerFactory);
            var clientFactory = new Http2ConnectionFactory();
            var endPoint = new UriEndPoint(new Uri("https://localhost:5003"));

            return (serverFactory, clientFactory, endPoint, endPoint);
        }

        public static async Task RunClientAsync(IConnectionFactory factory, EndPoint endpoint, CancellationToken cancellationToken = default)
        {
            var connection = await factory.ConnectAsync(endpoint);

            Console.WriteLine($"Connected to {endpoint}");
            Console.WriteLine("Echo server running, type into the console");

            var reading = Console.OpenStandardInput().CopyToAsync(connection.Transport.Output, cancellationToken);
            var writing = connection.Transport.Input.CopyToAsync(Console.OpenStandardOutput(), cancellationToken);

            await reading;
            await writing;
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
