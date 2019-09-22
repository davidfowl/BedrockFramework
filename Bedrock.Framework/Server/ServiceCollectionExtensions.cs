using System;
using Microsoft.Extensions.DependencyInjection;

namespace Bedrock.Framework
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection ConfigureServer(this IServiceCollection services, Action<ServerOptions> configure)
        {
            services.AddHostedService<Server>();
            services.AddOptions<ServerOptions>()
                    .Configure<IServiceProvider>((options, sp) =>
                    {
                        options.ApplicationServices = sp;
                        configure(options);
                    });
            return services;
        }
    }
}
