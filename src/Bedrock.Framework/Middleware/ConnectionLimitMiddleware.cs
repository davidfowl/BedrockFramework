using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace Bedrock.Framework;

public class ConnectionLimitMiddleware(ConnectionDelegate next, ILogger logger, int limit)
{
    private readonly SemaphoreSlim _limiter = new(limit);

    public async Task OnConnectionAsync(ConnectionContext connectionContext)
    {
        // Wait 10 seconds for a connection
        var task = _limiter.WaitAsync(TimeSpan.FromSeconds(10));

        if (!task.IsCompletedSuccessfully)
        {
            logger.LogInformation("{ConnectionId} queued", connectionContext.ConnectionId);

            if (!await task.ConfigureAwait(false))
            {
                logger.LogInformation("{ConnectionId} timed out in the connection queue", connectionContext.ConnectionId);
                return;
            }
        }

        try
        {
            await next(connectionContext).ConfigureAwait(false);
        }
        finally
        {
            _limiter.Release();
        }
    }
}
