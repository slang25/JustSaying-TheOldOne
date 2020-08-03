using System;
using System.Threading;
using System.Threading.Tasks;

namespace JustSaying.TestingFramework
{
    public static class TaskHelpers
    {
        private const int DefaultTimeoutMillis = 10000;
        private const int DelaySendMillis = 200;

        public static async Task<bool> WaitWithTimeoutAsync(Task task)
            => await WaitWithTimeoutAsync(task, TimeSpan.FromMilliseconds(DefaultTimeoutMillis))
                .ConfigureAwait(false);

        public static async Task<bool> WaitWithTimeoutAsync(Task task, TimeSpan timeoutDuration)
        {
            var timeoutTask = Task.Delay(timeoutDuration);
            var firstToComplete = await Task.WhenAny(task, timeoutTask).ConfigureAwait(false);

            if (firstToComplete != timeoutTask) return true;
            return false;
        }

        public static void DelaySendDone(TaskCompletionSource<object> doneSignal)
        {
            Task.Run(async () =>
            {
                await Task.Delay(DelaySendMillis).ConfigureAwait(false);
                doneSignal.SetResult(null);
            });
        }

        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            // This disposes the registration as soon as one of the tasks trigger
            using (cancellationToken.Register(state =>
                {
                    ((TaskCompletionSource<object>)state).TrySetResult(null);
                },
                tcs))
            {
                var resultTask = await Task.WhenAny(task, tcs.Task).ConfigureAwait(false);
                if (resultTask == tcs.Task)
                {
                    // Operation cancelled
                    throw new OperationCanceledException(cancellationToken);
                }

                return await task.ConfigureAwait(false);
            }
        }
    }
}
