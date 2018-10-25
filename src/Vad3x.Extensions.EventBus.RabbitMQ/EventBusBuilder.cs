using Microsoft.Extensions.DependencyInjection;

using Vad3x.Extensions.EventBus.Abstractions;

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
