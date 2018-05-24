namespace Vad3x.Extensions.EventBus.Abstractions
{
    public interface IEventPublisherEventExtender
    {
        void Extend(IntegrationEvent @event);
    }
}
