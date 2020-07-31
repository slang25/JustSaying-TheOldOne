using System.Threading.Tasks;
using JustSaying.AwsTools;
using JustSaying.AwsTools.MessageHandling;
using JustSaying.AwsTools.QueueCreation;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit.Abstractions;

namespace JustSaying.IntegrationTests.Fluent.AwsTools
{
    public class WhenCreatingTopicWithServerSideEncryption : IntegrationTestBase
    {
        public WhenCreatingTopicWithServerSideEncryption(ITestOutputHelper outputHelper)
            : base(outputHelper)
        {
        }

        [NotSimulatorFact]
        public async Task Can_Create_Topic_With_Encryption()
        {
            // Arrange
            ILoggerFactory loggerFactory = OutputHelper.ToLoggerFactory();
            IAwsClientFactory clientFactory = CreateClientFactory();

            var client = clientFactory.GetSnsClient(Region);

            var topic = new SnsTopicByName(
                UniqueName,
                client,
                null,
                loggerFactory,
                null);

            // Act
            await topic.CreateWithEncryptionAsync(new ServerSideEncryption { KmsMasterKeyId = JustSayingConstants.DefaultSnsAttributeEncryptionKeyId }).ConfigureAwait(false);

            // Assert
            topic.ServerSideEncryption.KmsMasterKeyId.ShouldBe(JustSayingConstants.DefaultSnsAttributeEncryptionKeyId);
        }

        [NotSimulatorFact]
        public async Task Can_Add_Encryption_To_Existing_Topic()
        {
            // Arrange
            ILoggerFactory loggerFactory = OutputHelper.ToLoggerFactory();
            IAwsClientFactory clientFactory = CreateClientFactory();

            var client = clientFactory.GetSnsClient(Region);

            var topic = new SnsTopicByName(
                UniqueName,
                client,
                null,
                loggerFactory,
                null);

            await topic.CreateAsync().ConfigureAwait(false);

            // Act
            await topic.CreateWithEncryptionAsync(new ServerSideEncryption { KmsMasterKeyId = JustSayingConstants.DefaultSnsAttributeEncryptionKeyId }).ConfigureAwait(false);

            // Assert
            topic.ServerSideEncryption.KmsMasterKeyId.ShouldBe(JustSayingConstants.DefaultSnsAttributeEncryptionKeyId);
        }

        [NotSimulatorFact]
        public async Task Can_Update_Encryption_For_Existing_Topic()
        {
            // Arrange
            ILoggerFactory loggerFactory = OutputHelper.ToLoggerFactory();
            IAwsClientFactory clientFactory = CreateClientFactory();

            var client = clientFactory.GetSnsClient(Region);

            var topic = new SnsTopicByName(
                UniqueName,
                client,
                null,
                loggerFactory,
                null);

            await topic.CreateWithEncryptionAsync(new ServerSideEncryption { KmsMasterKeyId = "previousKeyId" }).ConfigureAwait(false);

            // Act
            await topic.CreateWithEncryptionAsync(new ServerSideEncryption { KmsMasterKeyId = JustSayingConstants.DefaultSnsAttributeEncryptionKeyId }).ConfigureAwait(false);

            // Assert
            topic.ServerSideEncryption.KmsMasterKeyId.ShouldBe(JustSayingConstants.DefaultSnsAttributeEncryptionKeyId);
        }
    }
}
