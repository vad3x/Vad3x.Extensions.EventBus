using System;
using System.Collections.Generic;

namespace Vad3x.Extensions.EventBus.Abstractions
{
    public interface IEventBusSubscriptionsManager
    {
        bool IsEmpty { get; }

        event EventHandler<(string eventName, string exchangeName, string queueName)> OnEventRemoved;

        void AddSubscription<T, TH>(string exchangeName, string queueName)
           where T : IntegrationEvent
           where TH : IIntegrationEventHandler<T>;

        void AddSubscription(Type eventType, Type hanlderType, string exchangeName, string queueName);

        void RemoveSubscription<T, TH>()
             where TH : IIntegrationEventHandler<T>
             where T : IntegrationEvent;

        bool HasSubscriptionsForEvent<T>() where T : IntegrationEvent;

        bool HasSubscriptionsForEvent(string eventName);

        Type GetEventTypeByName(string eventName);

        void Clear();

        IEnumerable<SubscriptionInfo> GetHandlersForEvent<T>() where T : IntegrationEvent;

        IEnumerable<SubscriptionInfo> GetHandlersForEvent(string eventName);

        string GetEventKey<T>();

        string GetEventKey(Type eventType);
    }
}
