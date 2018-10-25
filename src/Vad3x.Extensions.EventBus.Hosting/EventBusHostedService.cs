using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vad3x.Extensions.EventBus.Abstractions;

namespace Vad3x.Extensions.EventBus.Hosting
{
    public class EventBusHostedService : IHostedService
    {
        private readonly ILogger<EventBusHostedService> _logger;
        private readonly IEventSubscriber _eventSubscriber;

        public EventBusHostedService(
            ILogger<EventBusHostedService> logger,
            IEventSubscriber eventSubscriber)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventSubscriber = eventSubscriber ?? throw new ArgumentNullException(nameof(eventSubscriber));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"'{nameof(EventBusHostedService)}' started");

            return _eventSubscriber.StartAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"'{nameof(EventBusHostedService)}' stopped");

            return _eventSubscriber.StopAsync(cancellationToken);
        }
    }
}
