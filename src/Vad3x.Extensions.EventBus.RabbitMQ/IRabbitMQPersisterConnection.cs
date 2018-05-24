using System;
using RabbitMQ.Client;

namespace Vad3x.Extensions.EventBus.RabbitMQ
{
    public interface IRabbitMQPersistentConnection : IDisposable
    {
        string ClientProvidedName { get; }

        bool IsConnected { get; }

        bool TryConnect();

        IModel CreateChannel();
    }
}
