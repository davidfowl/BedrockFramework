using System;
using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;

namespace BedrockTransports
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddServerApplication<TTransport>(this IServiceCollection services,
                                                                          EndPoint serverEndPoint,
                                                                          Action<IConnectionBuilder> serverApplication) where TTransport : IConnectionListenerFactory
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
                            ConnectionListenerFactory = ActivatorUtilities.CreateInstance<TTransport>(serviceProvider)
                        };
                        options.Bindings.Add(binding);
                    });

            services.AddHostedService<ServerApplication>();

            return services;
        }
    }
}
