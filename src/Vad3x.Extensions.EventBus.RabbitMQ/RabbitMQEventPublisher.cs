using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using Polly;

using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

using Vad3x.Extensions.EventBus.Abstractions;

namespace Vad3x.Extensions.EventBus.RabbitMQ
{
    public class RabbitMQEventPublisher : IEventPublisher
    {
        private readonly ILogger<RabbitMQEventPublisher> _logger;

        private readonly IEnumerable<IEventPublisherEventExtender> _eventExtenders;

        private readonly IRabbitMQPersistentConnection _persistentConnection;
        public RabbitMQEventPublisher(
            ILogger<RabbitMQEventPublisher> logger,
            IEnumerable<IEventPublisherEventExtender> eventExtenders,
            IRabbitMQPersistentConnection persistentConnection)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventExtenders = eventExtenders ?? throw new ArgumentNullException(nameof(eventExtenders));
            _persistentConnection = persistentConnection ?? throw new ArgumentNullException(nameof(persistentConnection));
        }

        public void Publish(IntegrationEvent @event, string exchangeName = null)
        {
            foreach (var extender in _eventExtenders)
            {
                extender.Extend(@event);
            }

            var connectStopwatch = Stopwatch.StartNew();
            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }

            connectStopwatch.Stop();

            var policy = Policy
                .Handle<BrokerUnreachableException>()
                .Or<SocketException>()
                .Or<TimeoutException>()
                .WaitAndRetryForever(retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                {
                    _logger.LogWarning(ex.ToString());
                });

            policy.Execute(() =>
            {
                var channelStopwatch = Stopwatch.StartNew();
                using (var channel = _persistentConnection.CreateChannel())
                {
                    channelStopwatch.Stop();

                    var properties = channel.CreateBasicProperties();
                    properties.Persistent = true;

                    var eventName = @event.GetType().Name;
                    var message = JsonConvert.SerializeObject(@event);
                    var body = Encoding.UTF8.GetBytes(message);

                    var publishStopwatch = Stopwatch.StartNew();
                    channel.BasicPublish(
                        exchange: exchangeName,
                        routingKey: eventName,
                        basicProperties: properties,
                        body: body);
                    publishStopwatch.Stop();

                    var totalElapsedMilliseconds
                        = connectStopwatch.ElapsedMilliseconds
                            + channelStopwatch.ElapsedMilliseconds
                            + publishStopwatch.ElapsedMilliseconds;

                    if (totalElapsedMilliseconds > 100)
                    {
                        _logger.LogWarning(
                            "Slow publishing to exchange: '{exchangeName}' event: '{eventType}' total: '{elapsedMilliseconds}'ms, connect: '{connectMilliseconds}'ms, channel: '{channelMilliseconds}'ms, publish: '{publishMilliseconds}'ms",
                            exchangeName,
                            @event.GetType().Name,
                            totalElapsedMilliseconds,
                            connectStopwatch.ElapsedMilliseconds,
                            channelStopwatch.ElapsedMilliseconds,
                            publishStopwatch.ElapsedMilliseconds);
                    }
                }
            });
        }
    }
}
