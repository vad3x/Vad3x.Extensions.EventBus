using System;

namespace Vad3x.Extensions.EventBus.Abstractions
{
    public class SubscriptionInfo
    {
        public Type HandlerType { get; }

        public string ExchangeName { get; }
        public string QueueName { get; }

        private SubscriptionInfo(Type handlerType, string exchangeName, string queueName)
        {
            HandlerType = handlerType;
            ExchangeName = exchangeName;
            QueueName = queueName;
        }

        public static SubscriptionInfo Typed(Type handlerType, string exchangeName, string queueName)
        {
            return new SubscriptionInfo(handlerType, exchangeName, queueName);
        }
    }
}
