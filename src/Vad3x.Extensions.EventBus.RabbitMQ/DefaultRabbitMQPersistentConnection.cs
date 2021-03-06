using System;
using System.IO;
using System.Net.Sockets;

using Microsoft.Extensions.Logging;

using Polly;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Vad3x.Extensions.EventBus.RabbitMQ
{
    public sealed class DefaultRabbitMQPersistentConnection : IRabbitMQPersistentConnection
    {
        private static readonly object sync_root = new object();

        private readonly ILogger<DefaultRabbitMQPersistentConnection> _logger;
        private readonly IConnectionFactory _connectionFactory;
        private IConnection _connection;
        private bool _disposed;

        public DefaultRabbitMQPersistentConnection(
            ILogger<DefaultRabbitMQPersistentConnection> logger,
            IConnectionFactory connectionFactory,
            string clientProvidedName,
            string[] exchanges,
            string[] queues,
            int retryPolicyMaxSleepDurationSeconds)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

            ClientProvidedName = clientProvidedName ?? throw new ArgumentNullException(nameof(clientProvidedName));
            Exchanges = exchanges ?? throw new ArgumentNullException(nameof(exchanges));
            Queues = queues ?? throw new ArgumentNullException(nameof(queues));
            RetryPolicyMaxSleepDurationSeconds = retryPolicyMaxSleepDurationSeconds;
        }

        public int RetryPolicyMaxSleepDurationSeconds { get; private set; }

        public string ClientProvidedName { get; private set; }

        public string[] Exchanges { get; private set; }

        public string[] Queues { get; private set; }

        public bool IsConnected
        {
            get
            {
                return _connection != null && _connection.IsOpen && !_disposed;
            }
        }

        public IModel CreateChannel()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("No RabbitMQ connections are available to perform this action");
            }

            return _connection.CreateModel();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                _connection.Dispose();
            }
            catch (IOException ex)
            {
                _logger.LogCritical(ex.ToString());
            }
        }

        public bool TryConnect()
        {
            _logger.LogInformation("RabbitMQ Client is trying to connect");

            lock (sync_root)
            {
                if (IsConnected)
                {
                    return true;
                }

                var policy = Policy
                    .Handle<SocketException>()
                    .Or<BrokerUnreachableException>()
                    .WaitAndRetryForever(retryAttempt =>
                    {
                        var seconds = Math.Pow(2, retryAttempt);
                        return TimeSpan.FromSeconds(seconds < RetryPolicyMaxSleepDurationSeconds ? seconds : RetryPolicyMaxSleepDurationSeconds);
                    }, (ex, time) =>
                    {
                        _logger.LogWarning(ex, "Could not establish connection to RabbitMQ because of `{exception}` in `{time}`, retrying...", ex.Message, time);
                    }
                );

                policy.Execute(() =>
                {
                    if (!_disposed)
                    {
                        _connection = _connectionFactory.CreateConnection(ClientProvidedName);
                    }
                    else
                    {
                        _logger.LogWarning("PersistentConnection is disposed, dropping creating RabbitMQ connection.");
                    }
                });

                if (IsConnected)
                {
                    _connection.ConnectionShutdown += OnConnectionShutdown;
                    _connection.CallbackException += OnCallbackException;
                    _connection.ConnectionBlocked += OnConnectionBlocked;

                    _logger.LogInformation($"RabbitMQ persistent connection acquired a connection '{_connection.Endpoint.HostName}' and is subscribed to failure events");

                    DeclareExchangesAndQueues();

                    return true;
                }
                else
                {
                    _logger.LogCritical("FATAL ERROR: RabbitMQ connections could not be created and opened");

                    return false;
                }
            }
        }

        private void DeclareExchangesAndQueues()
        {
            using (var channel = CreateChannel())
            {
                foreach (var exchangeName in Exchanges)
                {
                    channel.ExchangeDeclare(durable: true, exchange: exchangeName, type: "direct");
                }

                foreach (var queueName in Queues)
                {
                    channel.QueueDeclare(
                        queueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null);
                }
            }
        }

        private void OnConnectionBlocked(object sender, ConnectionBlockedEventArgs e)
        {
            if (_disposed)
                return;

            _logger.LogWarning("A RabbitMQ connection is shutdown. Trying to re-connect...");

            TryConnect();
        }

        void OnCallbackException(object sender, CallbackExceptionEventArgs e)
        {
            if (_disposed)
                return;

            _logger.LogWarning("A RabbitMQ connection throw exception. Trying to re-connect...");

            TryConnect();
        }

        void OnConnectionShutdown(object sender, ShutdownEventArgs reason)
        {
            if (_disposed)
                return;

            _logger.LogWarning("A RabbitMQ connection is on shutdown. Trying to re-connect...");

            TryConnect();
        }
    }
}
