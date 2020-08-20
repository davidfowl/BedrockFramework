using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework
{
    internal interface IEndPointPool
    {
        ValueTask ReturnAsync(Connection context);
        ValueTask<Connection> GetConnectionAsync(CancellationToken cancellationToken = default);
    }
}
