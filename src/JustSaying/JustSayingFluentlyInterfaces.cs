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
    public interface IAmJustSayingFluently
    {
        void StartListening(CancellationToken cancellationToken = default);
    }

    public interface IFluentSubscription
    {
        IHaveFulfilledSubscriptionRequirements WithMessageHandler<T>(
            SqsReadConfiguration subscriptionConfig,
            IHandlerResolver handlerResolver)
            where T : Message;

        IFluentSubscription ConfigureSubscriptionWith(Action<SqsReadConfiguration> config);
    }

    public interface IHaveFulfilledSubscriptionRequirements : IAmJustSayingFluently, IFluentSubscription
    {
    }

    public interface IHaveFulfilledPublishRequirements : IAmJustSayingFluently
    {
    }
}
