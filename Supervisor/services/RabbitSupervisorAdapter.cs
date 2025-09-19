using System.Text.Json;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore.Metadata;
using RabbitMQ.Client.Events;

namespace Supervisor.services;
using GatewayPluginContract;
using RabbitMQ.Client;


public class RabbitSupervisorAdapter : SupervisorAdapter
{
    private readonly IChannel _channel;
    private readonly Dictionary<SupervisorEventType, string> _exchangeNames;
    
    public RabbitSupervisorAdapter(IConfiguration configuration) : base(configuration)
    {
        ConnectionFactory factory;
        try
        {
            factory = new ConnectionFactory
            {
                HostName = configuration["Hostname"],
                UserName = configuration["Username"],
                Password = configuration["Password"],
                Port = int.TryParse(configuration["Port"], out var port) ? port : 5672,
            };
        }
        catch (Exception e)
        {
            throw new InvalidOperationException("Failed to create RabbitMQ connection. Check your RabbitMQ configuration options", e);
        }

        var connection = factory.CreateConnectionAsync().Result;
        _channel = connection.CreateChannelAsync().Result;
        
        // Declare exchanges
        _exchangeNames = new Dictionary<SupervisorEventType, string>
        {
            [SupervisorEventType.Command] = configuration["Queues:Commands"] ?? "commands",
            [SupervisorEventType.Event] = configuration["Queues:Events"] ?? "events",
            [SupervisorEventType.Heartbeat] = configuration["Queues:Heartbeats"] ?? "heartbeats",
            [SupervisorEventType.Request] = configuration["Queues:Requests"] ?? "requests",
            [SupervisorEventType.Response] = configuration["Queues:Responses"] ?? "responses",
            [SupervisorEventType.DeliveryUrl] = configuration["Queues:DeliveryUrls"] ?? "delivery_urls"
         };

        // Bind exchanges
        foreach (var exchangeName in _exchangeNames.Values)
        {
            _channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Fanout, durable: true, autoDelete: false);
        }
        _channel.BasicQosAsync(0, 1, false); // Fair dispatch
    }
    
    public override async Task SendEventAsync(SupervisorEvent eventData, Guid? instanceId = null, Guid? correlationId = null)
    {
        if (eventData == null)
        {
            throw new ArgumentNullException(nameof(eventData), "Event data cannot be null");
        }
        if (correlationId == null)
        {
            correlationId = Guid.NewGuid();
        }

        var body = System.Text.Encoding.UTF8.GetBytes(eventData.Value ?? "");
        var props = new BasicProperties()
        {
            CorrelationId = correlationId.ToString(),
        };

        await _channel.BasicPublishAsync(
            exchange: _exchangeNames[eventData.Type],
            routingKey: instanceId?.ToString() ?? "",
            mandatory: false,
            basicProperties: props,
            body: body
        );
    }
    
    public override async Task<SupervisorEvent> AwaitResponseAsync(Guid correlationId, TimeSpan timeout)
    {
        var queueName = _channel.QueueDeclareAsync().Result.QueueName;
        _channel.QueueBindAsync(queueName, _exchangeNames[SupervisorEventType.Response], "");

        var tcs = new TaskCompletionSource<SupervisorEvent>();
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            if (ea.BasicProperties.CorrelationId == correlationId.ToString())
            {
                var body = ea.Body.ToArray();
                var message = System.Text.Encoding.UTF8.GetString(body);
                var eventData = new SupervisorEvent
                {
                    Type = SupervisorEventType.Response,
                    Value = message,
                    CorrelationId = correlationId
                };
                tcs.SetResult(eventData);
            }
            await Task.Yield();
        };

        _channel.BasicConsumeAsync(queue: queueName, autoAck: true, consumer: consumer);

        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
        return completedTask == tcs.Task ? await tcs.Task : throw new TimeoutException("Awaiting response timed out");
    }

    public override Task SubscribeAsync(SupervisorEventType eventType, Func<SupervisorEvent, Task> handler, Guid? instanceId = null, Guid? correlationId = null)
    {
        var queueName = _channel.QueueDeclareAsync().Result.QueueName;
        _channel.QueueBindAsync(queueName, _exchangeNames[eventType], instanceId?.ToString() ?? "");

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            if (correlationId != null && ea.BasicProperties.CorrelationId != correlationId.ToString())
            {
                // Ignore messages that don't match the correlation ID
                return;
            }
            
            // Convert the received message to SupervisorEvent
            var body = ea.Body.ToArray();
            var message = System.Text.Encoding.UTF8.GetString(body);
            var eventData = new SupervisorEvent
            {
                Type = eventType,
                Value = message,
                CorrelationId = ea.BasicProperties.CorrelationId != null ? Guid.Parse(ea.BasicProperties.CorrelationId) : Guid.Empty
            };
            Console.WriteLine($"Received supervisor event: {eventData.Type} with value: {eventData.Value}");
            await handler(eventData);
            
        };

        _channel.BasicConsumeAsync(queue: queueName, autoAck: true, consumer: consumer);

        return Task.CompletedTask;
    }
}