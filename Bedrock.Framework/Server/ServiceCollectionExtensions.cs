using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bedrock.Framework
{
    public static class ServiceCollectionExtensions
    {
        public static IHostBuilder ConfigureServer(this IHostBuilder builder, Action<ServerOptions> configure)
        {
            return builder.ConfigureServices(services =>
            {
                services.AddHostedService<ServerHostedService>();

                services.AddOptions<ServerOptions>()
                        .Configure<IServiceProvider>((options, sp) =>
                        {
                            options.ApplicationServices = sp;
                            configure(options);
                        });
            });
        }
    }
}
