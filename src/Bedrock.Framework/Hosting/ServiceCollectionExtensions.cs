using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bedrock.Framework;

public static class ServiceCollectionExtensions
{
    public static IHostBuilder ConfigureServer(this IHostBuilder builder, Action<ServerBuilder> configure) => 
        builder.ConfigureServices(services => ConfigureServices(configure, services));

    public static IHostApplicationBuilder ConfigureServer(this IHostApplicationBuilder builder, Action<ServerBuilder> configure)
    {
        ConfigureServices(configure, builder.Services);
        return builder;
    }

    private static void ConfigureServices(Action<ServerBuilder> configure, IServiceCollection services)
    {
        services.AddHostedService<ServerHostedService>();

        services.AddOptions<ServerHostedServiceOptions>()
                .Configure<IServiceProvider>((options, sp) =>
                {
                    options.ServerBuilder = new ServerBuilder(sp);
                    configure(options.ServerBuilder);
                });
    }
}
