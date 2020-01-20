using Bedrock.Framework.Experimental.Protocols.Kafka.Services;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;

namespace Bedrock.Framework.Experimental.Protocols.Kafka
{
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddKafkaProtocol(this IServiceCollection container)
        {
            container.AddSingleton<KafkaProtocol>();
            container.AddSingleton<KafkaMessageReader>();
            container.AddSingleton<IKafkaConnectionManager, KafkaConnectionManager>();
            container.AddSingleton<KafkaMessageWriter>();
            container.AddTransient<ConnectionContext, ConnectionContextWithDelegate>();

            container.AddScoped<IMessageCorrelator, MessageCorrelator>();

            return container;
        }
    }
}
