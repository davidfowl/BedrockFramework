using System;
using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework
{
    public class NamedPipeConnectionListenerFactory : ConnectionListenerFactory
    {
        public override ValueTask<ConnectionListener> ListenAsync(EndPoint endPoint, IConnectionProperties options = null, CancellationToken cancellationToken = default)
        {
            if (!(endPoint is NamedPipeEndPoint np))
            {
                throw new NotSupportedException($"{endPoint.GetType()} is not supported");
            }
            var listener = new NamedPipeConnectionListener(np, options);
            return new ValueTask<ConnectionListener>(listener);
        }
    }
}
