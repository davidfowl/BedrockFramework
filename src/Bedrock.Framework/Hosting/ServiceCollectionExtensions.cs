using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bedrock.Framework
{
    public static class ServiceCollectionExtensions
    {
        public static IHostBuilder ConfigureServer(this IHostBuilder builder, Action<HostBuilderContext, ServerBuilder> configure)
        {
            return builder.ConfigureServices((context, services) =>
            {
                services.AddHostedService<ServerHostedService>();

                services.AddOptions<ServerHostedServiceOptions>()
                        .Configure<IServiceProvider>((options, sp) =>
                        {
                            options.ServerBuilder = new ServerBuilder(sp);
                            configure(context, options.ServerBuilder);
                        });
            });
        }
    }
}
