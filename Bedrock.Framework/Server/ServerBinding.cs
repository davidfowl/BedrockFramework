using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework
{
    public abstract class ServerBinding
    {
        public virtual ConnectionDelegate Application { get; protected set; }

        public abstract ValueTask<IConnectionListener> BindAsync(CancellationToken cancellationToken = default);
    }
}
