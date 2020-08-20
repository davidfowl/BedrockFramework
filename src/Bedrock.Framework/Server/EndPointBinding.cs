using System.Collections.Generic;
using System.Net;
using System.Net.Connections;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Bedrock.Framework
{
    public class EndPointBinding : ServerBinding
    {
        private readonly ConnectionDelegate _application;
        public EndPointBinding(EndPoint endPoint, ConnectionDelegate application, ConnectionListenerFactory connectionListenerFactory)
        {
            EndPoint = endPoint;
            _application = application;
            ConnectionListenerFactory = connectionListenerFactory;
        }

        private EndPoint EndPoint { get; }
        private ConnectionListenerFactory ConnectionListenerFactory { get; }

        public override ConnectionDelegate Application => _application;

        public override async IAsyncEnumerable<ConnectionListener> ListenAsync([EnumeratorCancellation]CancellationToken cancellationToken)
        {
            yield return await ConnectionListenerFactory.ListenAsync(EndPoint, null, cancellationToken);
        }

        public override string ToString()
        {
            return EndPoint?.ToString();
        }
    }
}
