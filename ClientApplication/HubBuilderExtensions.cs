using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace ClientApplication
{
    public static class HubBuilderExtensions
    {
        public static HubConnectionBuilder WithConnectionFactory(this HubConnectionBuilder hubConnectionBuilder, IConnectionFactory connectionFactory, EndPoint endPoint)
        {
            hubConnectionBuilder.Services.AddSingleton(connectionFactory);
            hubConnectionBuilder.Services.AddSingleton(endPoint);
            return hubConnectionBuilder;
        }
    }
}
