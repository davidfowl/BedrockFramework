        
namespace Bedrock.Framework
{
    public static class ClientExtensions
    {
        public static ClientBuilder UseConnectionPooling(this ClientBuilder builder)
        {
            // TODO right now, this must be called after UseSockets
            builder.Use(factory => new ConnectionPoolingFactory(factory));
            return builder;
        }
    }
}
