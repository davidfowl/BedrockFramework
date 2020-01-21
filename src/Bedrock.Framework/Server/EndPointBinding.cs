using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
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

        private EndPoint EndPoint { get; }
        private IConnectionListenerFactory ConnectionListenerFactory { get; }

        public override ConnectionDelegate Application { get; }

        public override async IAsyncEnumerable<IConnectionListener> BindAsync([EnumeratorCancellation]CancellationToken cancellationToken)
        {
            yield return await ConnectionListenerFactory.BindAsync(EndPoint, cancellationToken);
        }

        public override string? ToString()
        {
            return EndPoint?.ToString();
        }
    }
}
