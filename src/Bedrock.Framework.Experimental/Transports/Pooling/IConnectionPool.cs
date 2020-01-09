using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework
{
    public interface IConnectionPool
    {
        ValueTask<ConnectionContext> GetConnectionAsync(EndPoint endPoint, CancellationToken cancellationToken = default);
        ValueTask ReturnAsync(ConnectionContext context);
    }
}