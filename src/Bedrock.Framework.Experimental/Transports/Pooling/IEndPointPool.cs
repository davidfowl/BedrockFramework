using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework
{
    internal interface IEndPointPool
    {
        ValueTask ReturnAsync(ConnectionContext context);
        ValueTask<ConnectionContext> GetConnectionAsync(CancellationToken cancellationToken = default);
    }
}
