namespace Vad3x.Extensions.EventBus.Abstractions
{
    public interface IEventPublisher
    {
        void Publish(IntegrationEvent @event, string exchangeName = null);
    }
}
