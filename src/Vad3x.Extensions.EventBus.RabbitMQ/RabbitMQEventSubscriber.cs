using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Newtonsoft.Json;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using Vad3x.Extensions.EventBus.Abstractions;

namespace Vad3x.Extensions.EventBus.RabbitMQ
{
    public sealed class RabbitMQEventSubscriber : IEventSubscriber, IDisposable
    {
        private readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.DateTimeOffset
        };

        private readonly ILogger<RabbitMQEventSubscriber> _logger;
        private readonly RabbitMQOptions _options;

        private readonly IRabbitMQPersistentConnection _persistentConnection;
        private readonly IEventBusSubscriptionsManager _subsManager;
        private readonly IEnumerable<SubscriberInfo> _subscribers;
        private readonly IServiceProvider _serviceProvider;

        private IModel _consumerChannel;

        private readonly List<string> _bindedConsumerQueues = new List<string>();

        public RabbitMQEventSubscriber(
            ILogger<RabbitMQEventSubscriber> logger,
            IOptions<RabbitMQOptions> options,
            IServiceProvider serviceProvider,
            IRabbitMQPersistentConnection persistentConnection,
            IEventBusSubscriptionsManager subsManager,
            IEnumerable<SubscriberInfo> subscribers)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options.Value;
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _persistentConnection = persistentConnection ?? throw new ArgumentNullException(nameof(persistentConnection));
            _subsManager = subsManager ?? throw new ArgumentNullException(nameof(subsManager));
            _subscribers = subscribers;

            _subsManager.OnEventRemoved += SubsManager_OnEventRemoved;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Creating ConsumerChannel...");

            _consumerChannel = CreateConsumerChannel(_options.PrefetchCount);

            if (_subscribers != null)
            {
                foreach (var subscriber in _subscribers)
                {
                    _logger.LogInformation(
                        "Subscribe '{eventType}' -> '{handlerType}' on '{exchangeName}'/'{queueName}'",
                        subscriber.EventType.ToString(),
                        subscriber.HandlerType.ToString(),
                        subscriber.ExchangeName,
                        subscriber.QueueName);

                    Subscribe(subscriber.EventType, subscriber.HandlerType, subscriber.ExchangeName, subscriber.QueueName);
                }
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Disposing ConsumerChannel");

            Dispose();

            return Task.CompletedTask;
        }

        public void Subscribe<T, TH>(string exchangeName = null, string queueName = null)
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            DeclareConsumer(_consumerChannel, queueName);

            var eventName = _subsManager.GetEventKey<T>();

            _consumerChannel.QueueBind(
                queue: queueName,
                exchange: exchangeName,
                routingKey: eventName);

            var containsKey = _subsManager.HasSubscriptionsForEvent(eventName);
            if (!containsKey)
            {
                if (!_persistentConnection.IsConnected)
                {
                    _persistentConnection.TryConnect();
                }
            }

            _subsManager.AddSubscription<T, TH>(exchangeName, queueName);
        }

        public void Unsubscribe<T, TH>()
            where TH : IIntegrationEventHandler<T>
            where T : IntegrationEvent
        {
            _subsManager.RemoveSubscription<T, TH>();
        }

        public void Dispose()
        {
            if (_consumerChannel != null)
            {
                _consumerChannel.Dispose();
            }

            _subsManager.Clear();
        }

        private void SubsManager_OnEventRemoved(object sender, (string eventName, string exchangeName, string queueName) args)
        {
            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }

            using (var channel = _persistentConnection.CreateChannel())
            {
                channel.QueueUnbind(queue: args.queueName,
                    exchange: args.exchangeName,
                    routingKey: args.eventName);

                if (_subsManager.IsEmpty)
                {
                    _consumerChannel.Close();
                }
            }
        }

        private void DeclareConsumer(IModel channel, string queueName)
        {
            if (_bindedConsumerQueues.Contains(queueName))
            {
                return;
            }

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += async (model, ea) =>
            {
                var eventName = ea.RoutingKey;
                var message = Encoding.UTF8.GetString(ea.Body);

                try
                {
                    var success = await ProcessEventAsync(eventName, message);

                    if (success)
                    {
                        channel.BasicAck(ea.DeliveryTag, false);
                    }
                    else
                    {
                        channel.BasicNack(ea.DeliveryTag, false, true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogCritical("Event processing exception '{exceptionMessage}': {exception}", ex.Message, ex);

                    // To avoid crazy exception loops
                    await Task.Delay(1000);

                    // requeue
                    channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            channel.BasicConsume(
                queue: queueName,
                consumer: consumer,
                autoAck: false,
                consumerTag: $"{_persistentConnection.ClientProvidedName}-{Guid.NewGuid().ToString("n").Substring(0, 6)}");

            _bindedConsumerQueues.Add(queueName);

            channel.CallbackException += (sender, ea) =>
            {
                _consumerChannel.Dispose();
                _consumerChannel = CreateConsumerChannel(_options.PrefetchCount);
                DeclareConsumer(_consumerChannel, queueName);
            };
        }

        private IModel CreateConsumerChannel(ushort prefetchCount)
        {
            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }

            var channel = _persistentConnection.CreateChannel();
            channel.BasicQos(prefetchSize: 0, prefetchCount: prefetchCount, global: false);

            return channel;
        }

        private async Task<bool> ProcessEventAsync(string eventName, string message)
        {
            _logger.LogInformation("Processing '{eventName}'...", eventName);

            if (_subsManager.HasSubscriptionsForEvent(eventName))
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var subscriptions = _subsManager.GetHandlersForEvent(eventName);
                    foreach (var subscription in subscriptions)
                    {
                        _logger.LogInformation("Processing '{eventName}' on '{queueName}'", eventName, subscription.QueueName);

                        var eventType = _subsManager.GetEventTypeByName(eventName);
                        var integrationEvent = JsonConvert.DeserializeObject(message, eventType, _jsonSerializerSettings);
                        var handler = scope.ServiceProvider.GetService(subscription.HandlerType);

                        if (handler == null)
                        {
                            _logger.LogCritical("DI has not defined '{handlerType}'", subscription.HandlerType);
                            return false;
                        }

                        var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
                        await (Task)concreteType.GetMethod("Handle").Invoke(handler, new object[] { integrationEvent });
                    }
                }

                return true;
            }
            else
            {
                _logger.LogInformation("No subscription is found for '{eventName}' handling", eventName);
                return false;
            }
        }

        private void Subscribe(Type eventType, Type hanlderType, string exchangeName = null, string queueName = null)
        {
            DeclareConsumer(_consumerChannel, queueName);

            var eventName = _subsManager.GetEventKey(eventType);

            _consumerChannel.QueueBind(
                queue: queueName,
                exchange: exchangeName,
                routingKey: eventName);

            var containsKey = _subsManager.HasSubscriptionsForEvent(eventName);
            if (!containsKey)
            {
                if (!_persistentConnection.IsConnected)
                {
                    _persistentConnection.TryConnect();
                }
            }

            _subsManager.AddSubscription(eventType, hanlderType, exchangeName, queueName);
        }
    }
}
