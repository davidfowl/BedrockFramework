using System.Net;
using System.Security.Cryptography.X509Certificates;
using Bedrock.Framework;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServerApplication;

var builder = Host.CreateApplicationBuilder();

builder.Logging.SetMinimumLevel(LogLevel.Debug);

builder.Services.AddSignalR();

builder.ConfigureServer(server =>
{
    server.UseSockets(sockets =>
    {
        // Echo server
        sockets.ListenLocalhost(5000,
            builder => builder.UseConnectionLogging().UseConnectionHandler<EchoServerApplication>());

        // HTTP/1.1 server
        sockets.Listen(IPAddress.Loopback, 5001,
            builder => builder.UseConnectionLogging().UseConnectionHandler<HttpApplication>());

        // SignalR Hub
        sockets.Listen(IPAddress.Loopback, 5002,
            builder => builder.UseConnectionLogging().UseHub<Chat>());

        // MQTT application
        sockets.Listen(IPAddress.Loopback, 5003,
            builder => builder.UseConnectionLogging().UseConnectionHandler<MqttApplication>());

        // Echo Server with TLS
        sockets.Listen(IPAddress.Loopback, 5004,
            builder => builder.UseServerTls(options =>
            {
                options.LocalCertificate = X509CertificateLoader.LoadPkcs12FromFile("testcert.pfx", "testcert");

                // NOTE: Do not do this in a production environment
                options.AllowAnyRemoteCertificate();
            })
            .UseConnectionLogging().UseConnectionHandler<EchoServerApplication>());

        sockets.Listen(IPAddress.Loopback, 5005,
            builder => builder.UseConnectionLogging().UseConnectionHandler<MyCustomProtocol>());
    });
});

var host = builder.Build();

host.Run();