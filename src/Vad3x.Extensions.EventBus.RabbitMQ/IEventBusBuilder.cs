using Microsoft.Extensions.DependencyInjection;

namespace Vad3x.Extensions.EventBus.RabbitMQ
{
    public interface IEventBusBuilder
    {
        IServiceCollection Services { get; }
    }
}
