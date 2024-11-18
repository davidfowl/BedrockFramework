using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.AspNetCore.Connections;

namespace Bedrock.Framework;

public class EndPointBinding(EndPoint endPoint, ConnectionDelegate application, IConnectionListenerFactory connectionListenerFactory) : ServerBinding
{
    public override ConnectionDelegate Application => application;

    public override async IAsyncEnumerable<IConnectionListener> BindAsync([EnumeratorCancellation]CancellationToken cancellationToken)
    {
        yield return await connectionListenerFactory.BindAsync(endPoint, cancellationToken);
    }

    public override string ToString()
    {
        return endPoint?.ToString();
    }
}
