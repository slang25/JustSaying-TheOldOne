using System;
using System.Threading;
using JustSaying.AwsTools;
using JustSaying.AwsTools.QueueCreation;
using JustSaying.Messaging;
using JustSaying.Messaging.MessageHandling;
using JustSaying.Messaging.MessageSerialization;
using JustSaying.Messaging.Monitoring;
using JustSaying.Models;

namespace JustSaying
{
    public interface IAmJustSayingFluently : IMessagePublisher
    {
        IHaveFulfilledPublishRequirements WithSnsMessagePublisher<T>() where T : Message;
        IHaveFulfilledPublishRequirements WithSnsMessagePublisher<T>(Action<SnsWriteConfiguration> config) where T : Message;
        IHaveFulfilledPublishRequirements WithSqsMessagePublisher<T>(Action<SqsWriteConfiguration> config) where T : Message;

        /// <summary>
        /// Adds subscriber to topic.
        /// </summary>
        /// <param name="topicName">Topic name to subscribe to. If left empty,
        /// topic name will be message type name</param>
        /// <returns></returns>
        ISubscriberIntoQueue WithSqsTopicSubscriber(string topicName = null);

        ISubscriberIntoQueue WithSqsPointToPointSubscriber();

        void StartListening(CancellationToken cancellationToken = default);
    }

    public interface IFluentSubscription
    {
        IHaveFulfilledSubscriptionRequirements WithMessageHandler<T>(IHandlerResolver handlerResolver) where T : Message;

        IFluentSubscription ConfigureSubscriptionWith(Action<SqsReadConfiguration> config);
    }

    public interface IHaveFulfilledSubscriptionRequirements : IAmJustSayingFluently, IFluentSubscription
    {
    }

    public interface ISubscriberIntoQueue
    {
        IFluentSubscription IntoQueue(string queueName);
    }

    public interface IHaveFulfilledPublishRequirements : IAmJustSayingFluently
    {
    }
}
