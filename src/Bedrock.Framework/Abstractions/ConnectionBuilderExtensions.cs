using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Connections;
using System.Text;
using System.Threading.Tasks;

namespace Bedrock.Framework
{
    public static class ConnectionBuilderExtensions
    {
        public static IConnectionBuilder UseConnectionHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]TConnectionHandler>(this IConnectionBuilder connectionBuilder) where TConnectionHandler : ConnectionHandler
        {
            var handler = ActivatorUtilities.GetServiceOrCreateInstance<TConnectionHandler>(connectionBuilder.ApplicationServices);

            // This is a terminal middleware, so there's no need to use the 'next' parameter
            return connectionBuilder.Run(connection => handler.OnConnectedAsync(connection));
        }

        public static IConnectionBuilder Use(this IConnectionBuilder connectionBuilder, Func<Connection, Func<Task>, Task> middleware)
        {
            return connectionBuilder.Use(next =>
            {
                return context =>
                {
                    Func<Task> simpleNext = () => next(context);
                    return middleware(context, simpleNext);
                };
            });
        }

        public static IConnectionBuilder Run(this IConnectionBuilder connectionBuilder, Func<Connection, Task> middleware)
        {
            return connectionBuilder.Use(next =>
            {
                return context =>
                {
                    return middleware(context);
                };
            });
        }
    }
}
