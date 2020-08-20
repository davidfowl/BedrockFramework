using System.Collections.Generic;
using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework
{
    public abstract class ServerBinding
    {
        public virtual ConnectionDelegate Application { get; }

        public abstract IAsyncEnumerable<ConnectionListener> ListenAsync(CancellationToken cancellationToken = default);
    }
}
