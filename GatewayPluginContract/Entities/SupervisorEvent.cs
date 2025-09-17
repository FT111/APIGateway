namespace GatewayPluginContract.Entities;

public class SupervisorEvent : Entity
{
    public required SupervisorEventType Type { get; set; }
    public string? Value { get; set; }
    public Guid CorrelationId { get; set; }
}