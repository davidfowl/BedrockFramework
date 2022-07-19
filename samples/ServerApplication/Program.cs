using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Bedrock.Framework;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

namespace ServerApplication
{
    public partial class Program
    {
        public static async Task Main(string[] args)
        {
            // Manual wire up of the server
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddConsole();
            });

            services.AddSignalR();

            services.TryAddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
            services.TryAddSingleton<ObjectPool<StringBuilder>>(serviceProvider =>
            {
                var objectPoolProvider = serviceProvider.GetRequiredService<ObjectPoolProvider>();
                var policy = new StringBuilderPooledObjectPolicy();
                return objectPoolProvider.Create(policy);
            });

            var serviceProvider = services.BuildServiceProvider();

            var server = new ServerBuilder(serviceProvider)
                        .UseSockets(sockets =>
                        {
                            var stringBuilderPool = serviceProvider.GetRequiredService<ObjectPool<StringBuilder>>();

                            // Echo server
                            sockets.ListenLocalhost(5000,
                                builder => builder.UseConnectionLogging(stringBuilderPool: stringBuilderPool).UseConnectionHandler<EchoServerApplication>());

                            // HTTP/1.1 server
                            sockets.Listen(IPAddress.Loopback, 5001,
                                builder => builder.UseConnectionLogging(stringBuilderPool: stringBuilderPool).UseConnectionHandler<HttpApplication>());

                            // SignalR Hub
                            sockets.Listen(IPAddress.Loopback, 5002,
                                builder => builder.UseConnectionLogging(stringBuilderPool: stringBuilderPool).UseHub<Chat>());

                            // MQTT application
                            sockets.Listen(IPAddress.Loopback, 5003,
                                builder => builder.UseConnectionLogging(stringBuilderPool: stringBuilderPool).UseConnectionHandler<MqttApplication>());

                            // Echo Server with TLS
                            sockets.Listen(IPAddress.Loopback, 5004,
                                builder => builder.UseServerTls(options =>
                                {
                                    options.LocalCertificate = new X509Certificate2("testcert.pfx", "testcert");

                                    // NOTE: Do not do this in a production environment
                                    options.AllowAnyRemoteCertificate();
                                })
                                .UseConnectionLogging(stringBuilderPool: stringBuilderPool).UseConnectionHandler<EchoServerApplication>());

                            sockets.Listen(IPAddress.Loopback, 5005,
                                builder => builder.UseConnectionLogging().UseConnectionHandler<MyCustomProtocol>());
                        })
                        .Build();

            var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<Program>();

            await server.StartAsync();

            foreach (var ep in server.EndPoints)
            {
                logger.LogInformation("Listening on {EndPoint}", ep);
            }

            var tcs = new TaskCompletionSource<object>();
            Console.CancelKeyPress += (sender, e) =>
            {
                tcs.TrySetResult(null);
                e.Cancel = true;
            };

            await tcs.Task;

            await server.StopAsync();
        }
    }
}
