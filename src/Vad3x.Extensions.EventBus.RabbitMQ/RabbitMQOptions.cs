using Vad3x.Extensions.EventBus.Abstractions;
using System;
using System.Collections.Generic;

namespace Vad3x.Extensions.EventBus.RabbitMQ
{
    public class RabbitMQOptions
    {
        public string ClientProvidedName { get; set; }

        public string HostName { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }

        public string VirtualHost { get; set; }

        public ushort PrefetchCount { get; set; } = 1;

        public string[] Exchanges { get; set; }

        public string[] Queues { get; set; }
    }
}
