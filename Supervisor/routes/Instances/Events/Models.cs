using GatewayPluginContract;

namespace Supervisor.routes.Instances.Events;

public static class Models
{
    public class EventRequest
    {
        public required string Type { get; set; }
        public string? Value { get; set; }
    }
}

public static class Mapping
{
    
}