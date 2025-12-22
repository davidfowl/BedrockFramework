using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework;

public class SocketConnectionFactory : IConnectionFactory
{
    public ValueTask<ConnectionContext> ConnectAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
    {
        return new SocketConnection(endpoint).StartAsync();
    }
}
