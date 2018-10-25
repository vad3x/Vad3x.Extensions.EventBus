using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using RabbitMQ.Client;

using Vad3x.Extensions.EventBus.Abstractions;
using Vad3x.Extensions.EventBus.RabbitMQ;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IEventBusBuilder AddRabbitMQEventBus(
            this IServiceCollection services,
            Action<RabbitMQOptions> configureAction = null)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configureAction == null)
            {
                throw new ArgumentNullException(nameof(configureAction));
            }

            services.Configure(configureAction);

            services.AddSingleton<IRabbitMQPersistentConnection>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<RabbitMQOptions>>().Value;
                if (string.IsNullOrWhiteSpace(options.HostName))
                {
                    throw new ArgumentException($"HostName: '{options.HostName}' is not valid");
                }

                var logger = sp.GetRequiredService<ILogger<DefaultRabbitMQPersistentConnection>>();

                var subscribers = sp.GetRequiredService<IEnumerable<SubscriberInfo>>();

                var factory = new ConnectionFactory
                {
                    HostName = options.HostName
                };

                if (options.UserName != null)
                {
                    factory.UserName = options.UserName;
                }

                if (options.Password != null)
                {
                    factory.Password = options.Password;
                }

                if (options.VirtualHost != null)
                {
                    factory.VirtualHost = options.VirtualHost;
                }

                var exchanges = subscribers
                    .Select(x => x.ExchangeName)
                    .Union(options.Exchanges ?? Array.Empty<string>())
                    .Distinct()
                    .ToArray();

                var queues = subscribers.Select(x => x.QueueName).Distinct().ToArray();

                return new DefaultRabbitMQPersistentConnection(
                    logger,
                    factory,
                    options.ClientProvidedName,
                    exchanges,
                    queues,
                    options.RetryPolicyMaxSleepDurationSeconds);
            });

            services.AddSingleton<IEventPublisher, RabbitMQEventPublisher>();
            services.AddSingleton<IEventSubscriber, RabbitMQEventSubscriber>();
            services.AddSingleton<IEventBusSubscriptionsManager, InMemoryEventBusSubscriptionsManager>();

            return new EventBusBuilder(services);
        }
    }
}
