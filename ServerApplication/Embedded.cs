using System.Net;
using System.Threading.Tasks;
using Bedrock.Framework;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging.Abstractions;

namespace ServerApplication
{
    public class Embedded 
    {
        private readonly Server _server;

        public Embedded()
        {
            var options = new ServerOptions()
                   .Listen(IPAddress.Loopback, 5024, builder => builder.Run(OnConnection));

            _server = new Server(NullLoggerFactory.Instance, options);
        }

        public Task StartAsync()
        {
            return _server.StartAsync(default);
        }

        private Task OnConnection(ConnectionContext connection)
        {
            return connection.Transport.Input.CopyToAsync(connection.Transport.Output);
        }
    }
}
