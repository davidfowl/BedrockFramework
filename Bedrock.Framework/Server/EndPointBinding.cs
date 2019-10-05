using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework
{
    public class EndPointBinding : ServerBinding
    {
        public EndPointBinding(EndPoint endPoint, ConnectionDelegate application, IConnectionListenerFactory connectionListenerFactory)
        {
            EndPoint = endPoint;
            Application = application;
            ConnectionListenerFactory = connectionListenerFactory;
        }

        public EndPoint EndPoint { get; }
        public IConnectionListenerFactory ConnectionListenerFactory { get; }

        public override ValueTask<IConnectionListener> BindAsync(CancellationToken cancellationToken = default)
        {
            return ConnectionListenerFactory.BindAsync(EndPoint, cancellationToken);
        }

        public override string ToString()
        {
            return EndPoint?.ToString();
        }
    }
}
