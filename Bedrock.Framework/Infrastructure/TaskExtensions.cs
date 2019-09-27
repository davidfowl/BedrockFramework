using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bedrock.Framework
{
    internal static class TaskExtensions
    {
        public static async Task<bool> TimeoutAfter(this Task task, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource();
            var delayTask = Task.Delay(timeout, cts.Token);

            var resultTask = await Task.WhenAny(task, delayTask);
            if (resultTask == delayTask)
            {
                // Operation cancelled
                return false;
            }
            else
            {
                // Cancel the timer task so that it does not fire
                cts.Cancel();
            }

            await task;
            return true;
        }
    }
}
