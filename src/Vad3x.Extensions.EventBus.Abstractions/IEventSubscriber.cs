using System.Threading;
using System.Threading.Tasks;

namespace Vad3x.Extensions.EventBus.Abstractions
{
    public interface IEventSubscriber
    {
        Task StartAsync(CancellationToken cancellationToken = default);

        Task StopAsync(CancellationToken cancellationToken = default);

        void Subscribe<T, TH>(string exchangeName = null, string queueName = null)
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>;

        void Unsubscribe<T, TH>()
            where TH : IIntegrationEventHandler<T>
            where T : IntegrationEvent;
    }
}
