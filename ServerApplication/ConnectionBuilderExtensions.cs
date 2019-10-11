using Bedrock.Framework.Protocols;
using Microsoft.AspNetCore.Connections;

namespace ServerApplication
{
    public static partial class ConnectionBuilderExtensions
    {
        public static IConnectionBuilder UseHttpServer(this IConnectionBuilder builder, IHttpApplication application)
        {
            return builder.Run(connection =>
            {
                var httpConnection = HttpProtocol.CreateFromConnection(connection);
                return application.ProcessRequests(httpConnection.ReadAllRequestsAsync());
            });
        }
    }
}
