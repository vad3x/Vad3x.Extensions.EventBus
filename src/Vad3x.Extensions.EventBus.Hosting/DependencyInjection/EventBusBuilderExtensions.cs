using Vad3x.Extensions.EventBus.Abstractions;
using Vad3x.Extensions.EventBus.Hosting;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class EventBusBuilderExtensions
    {
        public static IEventBusBuilder AddEventBusHostedService(this IEventBusBuilder builder)
        {
            builder.Services.AddHostedService<EventBusHostedService>();

            return builder;
        }
    }
}
