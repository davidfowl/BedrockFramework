using System;
using System.IO;
using System.Net;
using System.Net.Connections;
using System.Net.Http;
using System.Threading.Tasks;
using Bedrock.Framework;
using Bedrock.Framework.Protocols;

namespace ServerApplication
{
    public class HttpApplication : ConnectionHandler
    {
        public override async Task OnConnectedAsync(Connection connection)
        {
            var httpConnection = new HttpServerProtocol(connection);

            while (true)
            {
                var request = await httpConnection.ReadRequestAsync();

                Console.WriteLine(request);

                // Consume the request body
                await request.Content.CopyToAsync(Stream.Null);

                await httpConnection.WriteResponseAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("Hello World")
                });
            }
        }
    }
}
