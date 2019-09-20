using System;
using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.DependencyInjection;

namespace BedrockTransports
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddWebSocketListener(this IServiceCollection services, Uri uri, Action<IConnectionBuilder> serverApplication)
        {
            return services.AddServerApplication<WebSocketConnectionListenerFactory>(new UriEndPoint(uri), serverApplication);
        }

        public static IServiceCollection AddHttp2Listener(this IServiceCollection services, Uri uri, Action<IConnectionBuilder> serverApplication)
        {
            return services.AddServerApplication<Http2ConnectionListenerFactory>(new UriEndPoint(uri), serverApplication);
        }

        public static IServiceCollection AddSocketListener(this IServiceCollection services, EndPoint endpoint, Action<IConnectionBuilder> serverApplication)
        {
            return services.AddServerApplication<SocketTransportFactory>(endpoint, serverApplication);
        }

        public static IServiceCollection AddAzureSignalRListener(this IServiceCollection services, string connectionString, string hub, Action<IConnectionBuilder> serverApplication)
        {
            return services.AddServerApplication<AzureSignalRConnectionListenerFactory>(
                new AzureSignalREndPoint(connectionString, hub, AzureSignalREndpointType.Server),
                serverApplication);
        }

        public static IServiceCollection AddServerApplication<TTransport>(this IServiceCollection services,
                                                                          EndPoint serverEndPoint,
                                                                          Action<IConnectionBuilder> serverApplication,
                                                                          params object[] args) where TTransport : IConnectionListenerFactory
        {
            services.AddOptions<ServerApplicationOptions>()
                    .Configure<IServiceProvider>((options, serviceProvider) =>
                    {
                        var serverBuilder = new ConnectionBuilder(serviceProvider);
                        serverApplication(serverBuilder);
                        var binding = new ServerBinding
                        {
                            ServerApplication = serverBuilder.Build(),
                            EndPoint = serverEndPoint,
                            ConnectionListenerFactory = ActivatorUtilities.CreateInstance<TTransport>(serviceProvider, args)
                        };
                        options.Bindings.Add(binding);
                    });

            services.AddHostedService<ServerApplication>();

            return services;
        }
    }
}
