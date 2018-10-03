using Vad3x.Extensions.EventBus.Abstractions;
using Vad3x.Extensions.EventBus.RabbitMQ;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    public class SubscriberInfo
    {
        public Type EventType { get; set; }

        public Type HandlerType { get; set; }

        public string ExchangeName { get; set; }

        public string QueueName { get; set; }
    }

    public static class EventBusBuilderExtensions
    {
        public static IEventBusBuilder AddSubscriber<TEvent, THandler>(
            this IEventBusBuilder builder,
            string exchangeName,
            string queueName)
            where TEvent : IntegrationEvent
            where THandler : class, IIntegrationEventHandler<TEvent>
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (exchangeName == null)
            {
                throw new ArgumentNullException(nameof(exchangeName));
            }

            if (queueName == null)
            {
                throw new ArgumentNullException(nameof(queueName));
            }

            builder.Services.AddTransient<THandler>();

            builder.Services.AddSingleton(new SubscriberInfo
            {
                EventType = typeof(TEvent),
                HandlerType = typeof(THandler),
                ExchangeName = exchangeName,
                QueueName = queueName
            });

            return builder;
        }
    }
}
