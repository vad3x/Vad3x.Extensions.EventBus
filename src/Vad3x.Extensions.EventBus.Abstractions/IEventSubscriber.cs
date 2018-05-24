namespace Vad3x.Extensions.EventBus.Abstractions
{
    public interface IEventSubscriber
    {
        void Subscribe<T, TH>(string exchangeName = null, string queueName = null)
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>;

        void Unsubscribe<T, TH>()
            where TH : IIntegrationEventHandler<T>
            where T : IntegrationEvent;
    }
}
