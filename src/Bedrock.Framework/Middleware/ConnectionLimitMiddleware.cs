using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace Bedrock.Framework
{
    public class ConnectionLimitMiddleware
    {
        private readonly ConnectionDelegate _next;
        private readonly SemaphoreSlim _limiter;
        private readonly ILogger _logger;

        public ConnectionLimitMiddleware(ConnectionDelegate next, ILogger logger, int limit)
        {
            _next = next;
            _logger = logger;
            _limiter = new SemaphoreSlim(limit);
        }

        public async Task OnConnectionAsync(ConnectionContext connectionContext)
        {
            // Wait 10 seconds for a connection
            var task = _limiter.WaitAsync(TimeSpan.FromSeconds(10));

#if (NETFRAMEWORK || NETSTANDARD2_0 || NETCOREAPP2_0)
            if(task.Status != TaskStatus.RanToCompletion)
#else
            if (!task.IsCompletedSuccessfully)
#endif
            {
                _logger.LogInformation("{ConnectionId} queued", connectionContext.ConnectionId);

                if (!await task.ConfigureAwait(false))
                {
                    _logger.LogInformation("{ConnectionId} timed out in the connection queue", connectionContext.ConnectionId);
                    return;
                }
            }

            try
            {
                await _next(connectionContext).ConfigureAwait(false);
            }
            finally
            {
                _limiter.Release();
            }
        }
    }
}
