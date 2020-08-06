using System;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using JustSaying.AwsTools;
using JustSaying.AwsTools.MessageHandling;
using JustSaying.AwsTools.QueueCreation;
using JustSaying.Messaging;
using JustSaying.Messaging.Channels;
using JustSaying.Messaging.Interrogation;
using JustSaying.Messaging.MessageHandling;
using JustSaying.Messaging.MessageSerialization;
using JustSaying.Messaging.Monitoring;
using JustSaying.Models;
using Microsoft.Extensions.Logging;

namespace JustSaying
{
    /// <summary>
    /// Fluently configure a JustSaying message bus.
    /// Intended usage:
    /// 1. Factory.JustSaying(); // Gimme a bus
    /// 2. WithMonitoring(instance) // Ensure you monitor the messaging status
    /// 3. Set subscribers - WithSqsTopicSubscriber() / WithSnsTopicSubscriber() etc // ToDo: Shouldn't be enforced in base! Is a JE concern.
    /// 3. Set Handlers - WithTopicMessageHandler()
    /// </summary>
    public class JustSayingFluently : ISubscriberIntoQueue,
        IHaveFulfilledSubscriptionRequirements,
        IHaveFulfilledPublishRequirements
    {
        private readonly ILogger _log;
        private readonly IVerifyAmazonQueues _amazonQueueCreator;
        private readonly IAwsClientFactoryProxy _awsClientFactoryProxy;
        protected internal IAmJustSaying Bus { get; set; }
        private SqsReadConfiguration _subscriptionConfig = new SqsReadConfiguration(SubscriptionType.ToTopic);
        private readonly ILoggerFactory _loggerFactory;

        protected internal JustSayingFluently(
            IAmJustSaying bus,
            IVerifyAmazonQueues queueCreator,
            IAwsClientFactoryProxy awsClientFactoryProxy,
            ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _log = _loggerFactory.CreateLogger("JustSaying");
            Bus = bus;
            _amazonQueueCreator = queueCreator;
            _awsClientFactoryProxy = awsClientFactoryProxy;
        }

        /// <summary>
        /// Register for publishing messages to SNS
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public IHaveFulfilledPublishRequirements WithSnsMessagePublisher<T>() where T : Message
        {
            return WithSnsMessagePublisher<T>(null);
        }

        /// <summary>
        /// Register for publishing messages to SNS
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public IHaveFulfilledPublishRequirements WithSnsMessagePublisher<T>(Action<SnsWriteConfiguration> configBuilder) where T : Message
        {
            return AddSnsMessagePublisher<T>(configBuilder);
        }

        private IHaveFulfilledPublishRequirements AddSnsMessagePublisher<T>(Action<SnsWriteConfiguration> configBuilder) where T : Message
        {
            _log.LogInformation("Adding SNS publisher for message type '{MessageType}'.",
                typeof(T));

            _subscriptionConfig = new SqsReadConfiguration(SubscriptionType.ToTopic);

            var snsWriteConfig = new SnsWriteConfiguration();
            configBuilder?.Invoke(snsWriteConfig);

            _subscriptionConfig.TopicName = GetOrUseTopicNamingConvention<T>(_subscriptionConfig.TopicName);

            Bus.SerializationRegister.AddSerializer<T>();

            foreach (var region in Bus.Config.Regions)
            {
                // TODO pass region down into topic creation for when we have foreign topics so we can generate the arn
                var eventPublisher = new SnsTopicByName(
                    _subscriptionConfig.TopicName,
                    _awsClientFactoryProxy.GetAwsClientFactory().GetSnsClient(RegionEndpoint.GetBySystemName(region)),
                    Bus.SerializationRegister,
                    _loggerFactory, snsWriteConfig,
                    Bus.Config.MessageSubjectProvider)
                {
                    MessageResponseLogger = Bus.Config.MessageResponseLogger
                };

                CreatePublisher(eventPublisher, snsWriteConfig);

                eventPublisher.EnsurePolicyIsUpdatedAsync(Bus.Config.AdditionalSubscriberAccounts).GetAwaiter().GetResult();

                Bus.AddMessagePublisher<T>(eventPublisher, region);
            }

            _log.LogInformation("Created SNS topic publisher on topic '{TopicName}' for message type '{MessageType}'.",
                _subscriptionConfig.TopicName, typeof(T));

            return this;
        }

        private static void CreatePublisher(SnsTopicByName eventPublisher, SnsWriteConfiguration snsWriteConfig)
        {
            if (snsWriteConfig.Encryption != null)
            {
                eventPublisher.CreateWithEncryptionAsync(snsWriteConfig.Encryption).GetAwaiter().GetResult();
            }
            else
            {
                eventPublisher.CreateAsync().GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Register for publishing messages to SQS
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public IHaveFulfilledPublishRequirements WithSqsMessagePublisher<T>(Action<SqsWriteConfiguration> configBuilder) where T : Message
        {
            _log.LogInformation("Adding SQS publisher for message type '{MessageType}'.",
                typeof(T));

            var config = new SqsWriteConfiguration();
            configBuilder?.Invoke(config);

            config.QueueName = GetOrUseQueueNamingConvention<T>(config.QueueName);

            foreach (var region in Bus.Config.Regions)
            {
                var regionEndpoint = RegionEndpoint.GetBySystemName(region);
                var sqsClient = _awsClientFactoryProxy.GetAwsClientFactory().GetSqsClient(regionEndpoint);

                var eventPublisher = new SqsPublisher(
                    regionEndpoint,
                    config.QueueName,
                    sqsClient,
                    config.RetryCountBeforeSendingToErrorQueue,
                    Bus.SerializationRegister,
                    _loggerFactory)
                {
                    MessageResponseLogger = Bus.Config.MessageResponseLogger
                };

                if (!eventPublisher.ExistsAsync().GetAwaiter().GetResult())
                {
                    eventPublisher.CreateAsync(config).GetAwaiter().GetResult();
                }

                Bus.AddMessagePublisher<T>(eventPublisher, region);
            }

            _log.LogInformation(
                "Created SQS publisher for message type '{MessageType}' on queue '{QueueName}'.",
                typeof(T),
                config.QueueName);

            return this;
        }

        /// <summary>
        /// I'm done setting up. Fire up listening on this baby...
        /// </summary>
        public void StartListening(CancellationToken cancellationToken = default)
        {
            Bus.MessageBackoffStrategy = _subscriptionConfig.MessageBackoffStrategy;
            _ = Bus.StartAsync(cancellationToken);
            _log.LogInformation("Started listening for messages");
        }

        /// <summary>
        /// Publish a message to the stack, asynchronously.
        /// </summary>
        /// <param name="message">The message to publish.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
        /// <returns>A <see cref="Task"/> that completes when the message has been published.</returns>
        public async Task PublishAsync(Message message, CancellationToken cancellationToken)
            => await PublishAsync(message, null, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Publish a message to the stack, asynchronously.
        /// </summary>
        /// <param name="message">The message to publish.</param>
        /// <param name="metadata">The <see cref="PublishMetadata"/> for this operation.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation.</param>
        /// <returns>A <see cref="Task"/> that completes when the message has been published.</returns>
        public virtual async Task PublishAsync(Message message, PublishMetadata metadata, CancellationToken cancellationToken)
        {
            if (Bus == null)
            {
                throw new InvalidOperationException("You must register for message publication before publishing a message");
            }

            await Bus.PublishAsync(message, metadata, cancellationToken)
                .ConfigureAwait(false);
        }

        public IFluentSubscription ConfigureSubscriptionWith(Action<SqsReadConfiguration> configBuilder)
        {
            configBuilder?.Invoke(_subscriptionConfig);
            return this;
        }

        public ISubscriberIntoQueue WithSqsTopicSubscriber(string topicName = null)
        {
            _subscriptionConfig = new SqsReadConfiguration(SubscriptionType.ToTopic)
            {
                TopicName = (topicName ?? string.Empty).ToLowerInvariant()
            };
            return this;
        }

        public ISubscriberIntoQueue WithSqsPointToPointSubscriber()
        {
            _subscriptionConfig = new SqsReadConfiguration(SubscriptionType.PointToPoint);
            return this;
        }

        public IFluentSubscription IntoQueue(string queueName)
        {
            _subscriptionConfig.QueueName = queueName;
            return this;
        }

        public IHaveFulfilledSubscriptionRequirements WithMessageHandler<T>(IHandlerResolver handlerResolver) where T : Message
        {
            if (handlerResolver is null) throw new ArgumentNullException(nameof(handlerResolver));

            _subscriptionConfig.TopicName = GetOrUseTopicNamingConvention<T>(_subscriptionConfig.TopicName);
            _subscriptionConfig.QueueName = GetOrUseQueueNamingConvention<T>(_subscriptionConfig.QueueName);
            _subscriptionConfig.SubscriptionGroupName ??= _subscriptionConfig.QueueName;

            var thing = _subscriptionConfig.SubscriptionType == SubscriptionType.PointToPoint
                ? PointToPointHandler<T>()
                : TopicHandler<T>();

            var resolutionContext = new HandlerResolutionContext(_subscriptionConfig.QueueName);
            var proposedHandler = handlerResolver.ResolveHandler<T>(resolutionContext);

            if (proposedHandler == null)
            {
                throw new HandlerNotRegisteredWithContainerException($"There is no handler for '{typeof(T)}' messages.");
            }

            Bus.AddMessageHandler(_subscriptionConfig.QueueName, () => handlerResolver.ResolveHandler<T>(resolutionContext));

            _log.LogInformation(
                "Added a message handler for message type for '{MessageType}' on topic '{TopicName}' and queue '{QueueName}'.",
                typeof(T),
                _subscriptionConfig.TopicName,
                _subscriptionConfig.QueueName);

            return thing;
        }

        private IHaveFulfilledSubscriptionRequirements TopicHandler<T>() where T : Message
        {
            _subscriptionConfig.PublishEndpoint = _subscriptionConfig.TopicName;
            _subscriptionConfig.TopicName = _subscriptionConfig.TopicName;
            _subscriptionConfig.QueueName = _subscriptionConfig.QueueName;

            _subscriptionConfig.Validate();

            foreach (string region in Bus.Config.Regions)
            {
                // TODO Make this async and remove GetAwaiter().GetResult() call
                var queue = _amazonQueueCreator.EnsureTopicExistsWithQueueSubscribedAsync(
                    region, Bus.SerializationRegister,
                    _subscriptionConfig,
                    Bus.Config.MessageSubjectProvider).GetAwaiter().GetResult();

                Bus.AddQueue(region,  _subscriptionConfig.SubscriptionGroupName, queue);

                _log.LogInformation(
                    "Created SQS topic subscription on topic '{TopicName}' and queue '{QueueName}'.",
                    _subscriptionConfig.TopicName,
                    _subscriptionConfig.QueueName);
            }

            return this;
        }

        private IHaveFulfilledSubscriptionRequirements PointToPointHandler<T>() where T : Message
        {
            _subscriptionConfig.QueueName = _subscriptionConfig.QueueName;

            foreach (var region in Bus.Config.Regions)
            {
                // TODO Make this async and remove GetAwaiter().GetResult() call
                var queue = _amazonQueueCreator.EnsureQueueExistsAsync(region, _subscriptionConfig).GetAwaiter().GetResult();

                Bus.AddQueue(region, _subscriptionConfig.SubscriptionGroupName, queue);

                _log.LogInformation(
                    "Created SQS subscriber for message type '{MessageType}' on queue '{QueueName}'.",
                    typeof(T),
                    _subscriptionConfig.QueueName);
            }
            return this;
        }


        private string GetOrUseTopicNamingConvention<T>(string overrideTopicName)
        {
            return string.IsNullOrWhiteSpace(overrideTopicName)
                ? Bus.Config.TopicNamingConvention.TopicName<T>()
                : overrideTopicName;
        }

        private string GetOrUseQueueNamingConvention<T>(string overrideQueueName)
        {
            return string.IsNullOrWhiteSpace(overrideQueueName)
                ? Bus.Config.QueueNamingConvention.QueueName<T>()
                : overrideQueueName;
        }

        public InterrogationResult Interrogate()
        {
            return Bus.Interrogate();
        }
    }
}
