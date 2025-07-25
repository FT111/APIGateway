namespace GatewayPluginContract;

public abstract class GatewayModel
{}

public class Pipe : GatewayModel
{
    public required Guid Id { get; set; }
    public int CreatedAt { get; set; }
    public int UpdatedAt { get; set; }
    public bool Global { get; set; }
    public ICollection<PipeService> Services { get; set; } = new List<PipeService>();
}

public class Endpoint : GatewayModel
{
    public required Guid Id { get; set; }
    public required string Path { get; set; }
    public required string TargetPath { get; set; }
    public required string TargetHost { get; set; }
    public required string TargetScheme { get; set; }
    public Pipe Pipe { get; set; } = null!;
}

public class PluginData : GatewayModel
{
    public int Id { get; set; }
    public required string Key { get; set; }
    public string Value { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Namespace { get; set; } = "global";
}

public class PipeService : GatewayModel
{
    public required string PluginName { get; set; }
    public required string PluginVersion { get; set; }
    public required string ServiceName { get; set; }
    public ServiceFailurePolicies FailurePolicy { get; set; }
    public Guid? EndpointId { get; set; }
    public Endpoint? Endpoint { get; set; }
}

public class PluginConfig : GatewayModel
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public required string Type { get; set; }
    public required string Namespace { get; set; } = "global";
    public Guid EndpointId { get; set; } = Guid.Empty;
    public bool Internal { get; set; } = false;
}