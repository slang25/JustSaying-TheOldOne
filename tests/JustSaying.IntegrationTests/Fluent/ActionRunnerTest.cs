using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace JustSaying.IntegrationTests.Fluent
{
    public class ActionRunnerTest : IntegrationTestBase
    {
        public ActionRunnerTest(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        protected override TimeSpan Timeout => TimeSpan.FromSeconds(2);

        [Fact]
        public async Task TestRunnerWillSucceedOnSuccessfulTask()
        {
            async Task SuccessTask(CancellationToken ctx) =>
                await Task.Delay(100, ctx).ConfigureAwait(false);

            await RunActionWithTimeout(SuccessTask).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestRunnerWillThrowOnTimeout()
        {
            async Task TimeoutTask(CancellationToken ctx) =>
                await Task.Delay(Timeout.Add(Timeout), ctx).ConfigureAwait(false);

            await Assert.ThrowsAsync<TimeoutException>(
                () => RunActionWithTimeout(TimeoutTask)).ConfigureAwait(false);
        }

        [Fact]
        public async Task TestRunnerWillThrowOnFailure()
        {
            async Task ThrowingTask(CancellationToken ctx)
            {
                await Task.Delay(100, ctx).ConfigureAwait(false);
                throw new InvalidOperationException();
            }

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => RunActionWithTimeout(ThrowingTask)).ConfigureAwait(false);
        }
    }
}
