using Microsoft.Extensions.DependencyInjection;

namespace Vad3x.Extensions.EventBus.RabbitMQ
{
    public class EventBusBuilder : IEventBusBuilder
    {
        public EventBusBuilder(IServiceCollection services)
        {
            Services = services;
        }

        public IServiceCollection Services { get; private set; }
    }
}
