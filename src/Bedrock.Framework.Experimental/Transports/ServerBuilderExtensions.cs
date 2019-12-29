using System;
using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;

namespace Bedrock.Framework
{
    public static partial class ServerBuilderExtensions
    {
        public static ServerBuilder ListenWebSocket(this ServerBuilder builder, Uri uri, Action<IConnectionBuilder> serverApplication)
        {
            return builder.Listen<WebSocketConnectionListenerFactory>(new UriEndPoint(uri), serverApplication);
        }

        public static ServerBuilder ListenHttp2(this ServerBuilder builder, Uri uri, Action<IConnectionBuilder> serverApplication)
        {
            return builder.Listen<Http2ConnectionListenerFactory>(new UriEndPoint(uri), serverApplication);
        }
    }
}
