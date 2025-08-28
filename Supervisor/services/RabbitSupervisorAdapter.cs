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
            [SupervisorEventType.Heartbeat] = configuration["Queues:Heartbeats"] ?? "heartbeats"
        };

        // Bind exchanges
        foreach (var exchangeName in _exchangeNames.Values)
        {
            _channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Fanout, durable: true, autoDelete: false);
        }
        _channel.BasicQosAsync(0, 1, false); // Fair dispatch
    }
    
    public override async Task SendEventAsync(SupervisorEvent eventData)
    {
        if (eventData == null)
        {
            throw new ArgumentNullException(nameof(eventData), "Event data cannot be null");
        }

        var body = System.Text.Encoding.UTF8.GetBytes(eventData.Value ?? "");
        await _channel.BasicPublishAsync(
            exchange: _exchangeNames[eventData.Type],
            routingKey: "",
            body: body
        );
    }

    public override Task SubscribeAsync(SupervisorEventType eventType, Func<SupervisorEvent, Task> handler)
    {
        var queueName = _channel.QueueDeclareAsync().Result.QueueName;
        _channel.QueueBindAsync(queueName, _exchangeNames[eventType], "");

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            // Convert the received message to SupervisorEvent
            var body = ea.Body.ToArray();
            var message = System.Text.Encoding.UTF8.GetString(body);
            var eventData = new SupervisorEvent
            {
                Type = eventType,
                Value = message
            };
            Console.WriteLine($"Received supervisor event: {eventData.Type} with value: {eventData.Value}");
            await handler(eventData);
            
        };

        _channel.BasicConsumeAsync(queue: queueName, autoAck: true, consumer: consumer);

        return Task.CompletedTask;
    }
}