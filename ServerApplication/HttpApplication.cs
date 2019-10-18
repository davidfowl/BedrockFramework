using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Bedrock.Framework.Protocols;

namespace ServerApplication
{
    public class HttpApplication : IHttpApplication
    {
        public async Task ProcessRequests(IAsyncEnumerable<IHttpContext> requests)
        {
            var responseData = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 11\r\n\r\nHello World");

            await foreach (var context in requests)
            {
                await context.Output.WriteAsync(responseData);
            }
        }
    }
}
