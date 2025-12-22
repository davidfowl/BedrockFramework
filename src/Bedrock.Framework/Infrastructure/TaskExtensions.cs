using System;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework;

internal static class TaskExtensions
{
    public static async Task<bool> WithCancellation(this Task task, CancellationToken cancellationToken)
    {
        try
        {
            await task.WaitAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public static async Task<bool> TimeoutAfter(this Task task, TimeSpan timeout)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeout);
            await task.WaitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
