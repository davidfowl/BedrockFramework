using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.Hosting;

namespace BedrockTransports
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder()
                           .ConfigureServices(services =>
                           {
                               services.AddServerApplication<WebSocketConnectionListenerFactory>(
                                   new UriEndPoint(new Uri("https://localhost:5003")),
                                   builder => builder.UseConnectionHandler<EchoServer>());

                               services.AddServerApplication<Http2ConnectionListenerFactory>(
                                   new UriEndPoint(new Uri("https://localhost:5004")),
                                   builder => builder.UseConnectionHandler<EchoServer>());

                               services.AddServerApplication<SocketTransportFactory>(
                                   new IPEndPoint(IPAddress.Loopback, 5005),
                                   builder => builder.UseConnectionHandler<EchoServer>());

                               // This is a transport based on the AzureSignalR protocol, it gives you a full duplex mutliplexed connection over the 
                               // the internet
                               // Put your azure SignalR connection string here (securely of course!)

                               // var connectionString = "";
                               // services.AddServerApplication<AzureSignalRConnectionListenerFactory>(
                               //    new AzureSignalREndPoint(connectionString, "myhub", AzureSignalREndpointType.Server),
                               //    builder => builder.UseConnectionHandler<EchoServer>());

                           })
                           .Build();

            await host.RunAsync();
        }
    }
}
