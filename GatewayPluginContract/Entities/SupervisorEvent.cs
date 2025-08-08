namespace GatewayPluginContract.Entities;

public class SupervisorEvent
{
    public required SupervisorEventType Type { get; set; }
    public string? Value { get; set; }
}