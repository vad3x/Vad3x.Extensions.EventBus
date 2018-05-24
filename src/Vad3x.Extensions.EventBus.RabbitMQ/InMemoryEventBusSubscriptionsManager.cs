using System;
using System.Collections.Generic;
using System.Linq;
using Vad3x.Extensions.EventBus.Abstractions;

namespace Vad3x.Extensions.EventBus.RabbitMQ
{
    public partial class InMemoryEventBusSubscriptionsManager : IEventBusSubscriptionsManager
    {
        private readonly Dictionary<string, List<SubscriptionInfo>> _handlers;
        private readonly List<Type> _eventTypes;

        public event EventHandler<(string eventName, string exchangeName, string queueName)> OnEventRemoved;

        public InMemoryEventBusSubscriptionsManager()
        {
            _handlers = new Dictionary<string, List<SubscriptionInfo>>();
            _eventTypes = new List<Type>();
        }

        public bool IsEmpty => !_handlers.Keys.Any();
        public void Clear() => _handlers.Clear();

        public void AddSubscription<TEvent, TH>(string exchangeName, string queueName)
            where TEvent : IntegrationEvent
            where TH : IIntegrationEventHandler<TEvent>
        {
            AddSubscription(typeof(TEvent), typeof(TH), exchangeName, queueName);
        }

        public void AddSubscription(Type eventType, Type hanlderType, string exchangeName, string queueName)
        {
            var eventName = GetEventKey(eventType);
            DoAddSubscription(hanlderType, eventName, exchangeName, queueName);
            _eventTypes.Add(eventType);
        }

        public void RemoveSubscription<T, TH>()
            where TH : IIntegrationEventHandler<T>
            where T : IntegrationEvent
        {
            var handlerToRemove = FindSubscriptionToRemove<T, TH>();
            var eventName = GetEventKey<T>();
            DoRemoveHandler(eventName, handlerToRemove);
        }

        public IEnumerable<SubscriptionInfo> GetHandlersForEvent<T>() where T : IntegrationEvent
        {
            var key = GetEventKey<T>();
            return GetHandlersForEvent(key);
        }

        public IEnumerable<SubscriptionInfo> GetHandlersForEvent(string eventName) => _handlers[eventName];

        public bool HasSubscriptionsForEvent<T>() where T : IntegrationEvent
        {
            var key = GetEventKey<T>();
            return HasSubscriptionsForEvent(key);
        }

        public bool HasSubscriptionsForEvent(string eventName) => _handlers.ContainsKey(eventName);

        public Type GetEventTypeByName(string eventName) => _eventTypes.SingleOrDefault(t => t.Name == eventName);

        public string GetEventKey<T>()
        {
            return GetEventKey(typeof(T));
        }

        public string GetEventKey(Type eventType)
        {
            return eventType.Name;
        }

        private void DoAddSubscription(Type handlerType, string eventName, string exchangeName, string queueName)
        {
            if (!HasSubscriptionsForEvent(eventName))
            {
                _handlers.Add(eventName, new List<SubscriptionInfo>());
            }

            if (_handlers[eventName].Any(s => s.HandlerType == handlerType))
            {
                throw new ArgumentException(
                    $"Handler Type {handlerType.Name} already registered for '{eventName}'", nameof(handlerType));
            }

            _handlers[eventName].Add(SubscriptionInfo.Typed(handlerType, exchangeName, queueName));
        }

        private void DoRemoveHandler(string eventName, SubscriptionInfo subToRemove)
        {
            if (subToRemove != null)
            {
                _handlers[eventName].Remove(subToRemove);
                if (!_handlers[eventName].Any())
                {
                    _handlers.Remove(eventName);
                    var eventType = _eventTypes.SingleOrDefault(e => e.Name == eventName);
                    if (eventType != null)
                    {
                        _eventTypes.Remove(eventType);
                    }
                    RaiseOnEventRemoved(eventName, subToRemove.ExchangeName, subToRemove.QueueName);
                }

            }
        }

        private void RaiseOnEventRemoved(string eventName, string exchangeName, string queueName)
        {
            var handler = OnEventRemoved;
            if (handler != null)
            {
                OnEventRemoved(this, (eventName, exchangeName, queueName));
            }
        }

        private SubscriptionInfo FindSubscriptionToRemove<T, TH>()
             where T : IntegrationEvent
             where TH : IIntegrationEventHandler<T>
        {
            var eventName = GetEventKey<T>();
            return DoFindSubscriptionToRemove(eventName, typeof(TH));
        }

        private SubscriptionInfo DoFindSubscriptionToRemove(string eventName, Type handlerType)
        {
            if (!HasSubscriptionsForEvent(eventName))
            {
                return null;
            }

            return _handlers[eventName].SingleOrDefault(s => s.HandlerType == handlerType);

        }
    }
}
