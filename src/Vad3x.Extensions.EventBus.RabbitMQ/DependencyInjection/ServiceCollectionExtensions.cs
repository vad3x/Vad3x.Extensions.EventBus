using System;
using Vad3x.Extensions.EventBus.Abstractions;
using Vad3x.Extensions.EventBus.RabbitMQ;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Collections.Generic;
using System.Linq;

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
                var logger = sp.GetRequiredService<ILogger<DefaultRabbitMQPersistentConnection>>();
                var options = sp.GetRequiredService<IOptions<RabbitMQOptions>>().Value;

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

                var exchanges = subscribers.Select(x => x.ExchangeName).Distinct().ToArray();
                var queues = subscribers.Select(x => x.QueueName).Distinct().ToArray();

                return new DefaultRabbitMQPersistentConnection(logger, factory, options.ClientProvidedName, exchanges, queues);
            });

            services.AddSingleton<IEventPublisher, RabbitMQEventPublisher>();
            services.AddSingleton<IEventSubscriber, RabbitMQEventSubscriber>();
            services.AddSingleton<IEventBusSubscriptionsManager, InMemoryEventBusSubscriptionsManager>();

            return new EventBusBuilder(services);
        }
    }
}
