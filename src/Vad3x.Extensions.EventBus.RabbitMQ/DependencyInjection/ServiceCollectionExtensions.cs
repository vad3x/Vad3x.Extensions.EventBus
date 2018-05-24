using System;
using Vad3x.Extensions.EventBus.Abstractions;
using Vad3x.Extensions.EventBus.RabbitMQ;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

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

                return new DefaultRabbitMQPersistentConnection(logger, factory, options.ClientProvidedName, options.Exchanges ?? Array.Empty<string>(), options.Queues ?? Array.Empty<string>());
            });

            services.AddSingleton<IEventPublisher, RabbitMQEventPublisher>();
            services.AddSingleton<IEventSubscriber, RabbitMQEventSubscriber>();
            services.AddSingleton<IEventBusSubscriptionsManager, InMemoryEventBusSubscriptionsManager>();

            return new EventBusBuilder(services);
        }
    }
}
