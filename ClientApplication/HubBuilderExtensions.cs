using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Bedrock.Framework;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.SignalR.Client
{
    public static class HubBuilderExtensions
    {
        public static IHubConnectionBuilder WithConnectionFactory(this IHubConnectionBuilder hubConnectionBuilder, IConnectionFactory connectionFactory, EndPoint endPoint)
        {
            hubConnectionBuilder.Services.AddSingleton(connectionFactory);
            hubConnectionBuilder.Services.AddSingleton(endPoint);
            return hubConnectionBuilder;
        }

        public static IHubConnectionBuilder WithClientBuilder(this IHubConnectionBuilder hubConnectionBuilder, EndPoint endPoint, Action<ClientBuilder> configure)
        {
            hubConnectionBuilder.Services.AddSingleton<IConnectionFactory>(sp =>
            {
                var builder = new ClientBuilder(sp);
                configure(builder);
                return builder.Build();
            });

            hubConnectionBuilder.Services.AddSingleton(endPoint);
            return hubConnectionBuilder;
        }
    }
}
