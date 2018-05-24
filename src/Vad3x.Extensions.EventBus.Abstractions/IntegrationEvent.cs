using System;

namespace Vad3x.Extensions.EventBus.Abstractions
{
    public abstract class IntegrationEvent
    {
        protected IntegrationEvent()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
        }

        public Guid Id { get; }

        public string CorrelationId { get; set; }

        public DateTime CreatedAt { get; }
    }
}
