using Microsoft.Extensions.DependencyInjection;

namespace Vad3x.Extensions.EventBus.Abstractions
{
    public interface IEventBusBuilder
    {
        IServiceCollection Services { get; }
    }
}
