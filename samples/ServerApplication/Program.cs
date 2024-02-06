using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Bedrock.Framework;
using Microsoft.AspNetCore.Connections;
//using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

            //services.AddSignalR();

            var serviceProvider = services.BuildServiceProvider();

            var server = new ServerBuilder(serviceProvider)
                        .UseNamedPipes(namedPipes =>
                        {
                            // Echo server
                            namedPipes.Listen("default_echo",
                                builder => builder.UseConnectionLogging().UseConnectionHandler<EchoServerApplication>());

                            // HTTP/1.1 server
                            //namedPipes.Listen("default_http11",
                            //    builder => builder.UseConnectionLogging().UseConnectionHandler<HttpApplication>());

                            // SignalR Hub
                            //namedPipes.Listen("default_signalr",
                            //    builder => builder.UseConnectionLogging().UseHub<Chat>());

                            // MQTT application
                            namedPipes.Listen("default_mqtt",
                                builder => builder.UseConnectionLogging().UseConnectionHandler<MqttApplication>());

                            // Echo Server with TLS
                            //namedPipes.Listen("default_tls"
                            //    builder => builder.UseServerTls(options =>
                            //    {
                            //        options.LocalCertificate = new X509Certificate2("testcert.pfx", "testcert");

                            //        // NOTE: Do not do this in a production environment
                            //        options.AllowAnyRemoteCertificate();
                            //    })
                            //    .UseConnectionLogging().UseConnectionHandler<EchoServerApplication>());

                            namedPipes.Listen("default",
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
