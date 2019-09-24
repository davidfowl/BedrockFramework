using Microsoft.AspNetCore.Connections;

namespace ServerApplication
{
    public static partial class ConnectionBuilderExtensions
    {
        public static IConnectionBuilder UseHttpServer(this IConnectionBuilder builder, IHttpApplication application)
        {
            return builder.Run(connection =>
            {
                var asyncEnumerable = new HttpConnection(connection).RunAsync();
                return application.ProcessRequests(asyncEnumerable);
            });
        }
    }
}
