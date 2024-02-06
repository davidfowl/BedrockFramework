using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework
{
    public class NamedPipeConnectionListenerFactory : IConnectionListenerFactory
    {
        public ValueTask<IConnectionListener> BindAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
        {
            if (!(endpoint is NamedPipeEndPoint np))
            {
                throw new NotSupportedException($"{endpoint.GetType()} is not supported");
            }
            var listener = new NamedPipeConnectionListener(np);
            return new ValueTask<IConnectionListener>(listener);
        }
    }
}
