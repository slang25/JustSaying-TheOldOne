using System.Threading.Tasks;
using JustSaying.AwsTools;
using JustSaying.AwsTools.MessageHandling;
using JustSaying.AwsTools.QueueCreation;
using JustSaying.TestingFramework;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace JustSaying.IntegrationTests.Fluent.AwsTools
{
    public class WhenANamedQueueIsCreated : IntegrationTestBase
    {
        public WhenANamedQueueIsCreated(ITestOutputHelper outputHelper)
            : base(outputHelper)
        {
        }

        [AwsFact]
        public async Task Then_The_Error_Queue_Is_Created()
        {
            // Arrange
            ILoggerFactory loggerFactory = OutputHelper.ToLoggerFactory();
            IAwsClientFactory clientFactory = CreateClientFactory();

            var client = clientFactory.GetSqsClient(Region);

            var queue = new SqsQueueByName(
                Region,
                UniqueName,
                client,
                1,
                loggerFactory);

            // Act
            await queue.CreateAsync(new SqsBasicConfiguration()).ConfigureAwait(false);

            // Assert
            await Patiently.AssertThatAsync(
                async () => await queue.ErrorQueue.ExistsAsync().ConfigureAwait(false),
                40.Seconds()).ConfigureAwait(false);
        }
    }
}
