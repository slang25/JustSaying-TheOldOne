using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using JustSaying.AwsTools.MessageHandling;
using JustSaying.Messaging.Channels.SubscriptionGroups;
using JustSaying.Messaging.MessageSerialization;
using JustSaying.Messaging.Monitoring;
using JustSaying.TestingFramework;
using JustSaying.UnitTests.Messaging.Channels.TestHelpers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace JustSaying.UnitTests.Messaging.Channels
{
    public class SubscriptionGroupCollectionTests
    {
        private ILoggerFactory LoggerFactory { get; }
        private IMessageMonitor MessageMonitor { get; }

        public SubscriptionGroupCollectionTests(ITestOutputHelper testOutputHelper)
        {
            LoggerFactory = testOutputHelper.ToLoggerFactory();
            MessageMonitor = new LoggingMonitor(LoggerFactory.CreateLogger<IMessageMonitor>());
        }

        private static readonly TimeSpan TimeoutPeriod = TimeSpan.FromSeconds(1);

        [Fact]
        public async Task Add_Different_Handler_Per_Queue()
        {
            // Arrange
            string group1 = "group1";
            string group2 = "group2";
            string region = "region";
            string queueName1 = "queue1";
            string queueName2 = "queue2";

            JustSaying.JustSayingBus bus = CreateBus();

            ISqsQueue queue1 = TestQueue(bus.SerializationRegister, queueName1);
            ISqsQueue queue2 = TestQueue(bus.SerializationRegister, queueName2);

            bus.AddQueue(region, group1, queue1);
            bus.AddQueue(region, group2, queue2);

            var handledBy1 = new List<TestJustSayingMessage>();
            var handledBy2 = new List<TestJustSayingMessage>();

            bus.AddMessageHandler(queueName1, () => new TestHandler<TestJustSayingMessage>(x => handledBy1.Add(x)));
            bus.AddMessageHandler(queueName2, () => new TestHandler<TestJustSayingMessage>(x => handledBy2.Add(x)));

            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeoutPeriod);

            // Act
            await bus.StartAsync(cts.Token);
            await cts.Token.WaitForCancellation();

            // Assert
            handledBy1.Count.ShouldBeGreaterThan(0);
            foreach (var message in handledBy1)
            {
                message.QueueName.ShouldBe(queueName1);
            }

            handledBy2.Count.ShouldBeGreaterThan(0);
            foreach (var message in handledBy2)
            {
                message.QueueName.ShouldBe(queueName2);
            }
        }

        private JustSaying.JustSayingBus CreateBus()
        {
            var config = Substitute.For<IMessagingConfig>();
            var serializationRegister = new MessageSerializationRegister(
                new NonGenericMessageSubjectProvider(),
                new NewtonsoftSerializationFactory());

            var bus = new JustSaying.JustSayingBus(config, serializationRegister, LoggerFactory)
            {
                Monitor = MessageMonitor,
            };

            var defaultSubscriptionSettings = new SubscriptionGroupSettingsBuilder()
                .WithDefaultMultiplexerCapacity(1)
                .WithDefaultPrefetch(1)
                .WithDefaultBufferSize(1)
                .WithDefaultConcurrencyLimit(1); // N

            bus.SetGroupSettings(defaultSubscriptionSettings, new Dictionary<string, SubscriptionGroupConfigBuilder>());

            bus.SerializationRegister.AddSerializer<TestJustSayingMessage>();

            return bus;
        }

        private static ISqsQueue TestQueue(
            IMessageSerializationRegister messageSerializationRegister,
            string queueName,
            Action spy = null)
        {
            ReceiveMessageResponse GetMessages()
            {
                spy?.Invoke();
                var message = new TestJustSayingMessage
                {
                    QueueName = queueName,
                };

                var messages = new List<Message>
                {
                    new TestMessage { Body = messageSerializationRegister.Serialize(message, false) },
                };

                return new ReceiveMessageResponse { Messages = messages };
            }

            IAmazonSQS sqsClientMock = Substitute.For<IAmazonSQS>();
            sqsClientMock
                .ReceiveMessageAsync(Arg.Any<ReceiveMessageRequest>(), Arg.Any<CancellationToken>())
                .Returns(_ => GetMessages());

            ISqsQueue sqsQueueMock = Substitute.For<ISqsQueue>();
            sqsQueueMock.Uri.Returns(new Uri("http://test.com"));
            sqsQueueMock.Client.Returns(sqsClientMock);
            sqsQueueMock.QueueName.Returns(queueName);
            sqsQueueMock.Uri.Returns(new Uri("http://foo.com"));

            return sqsQueueMock;
        }

        private class TestMessage : Message
        {
            public override string ToString()
            {
                return Body;
            }
        }

        private class TestJustSayingMessage : JustSaying.Models.Message
        {
            public string QueueName { get; set; }

            public override string ToString()
            {
                return QueueName;
            }
        }
    }
}
