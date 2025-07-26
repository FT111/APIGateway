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
    public ICollection<PluginConfig> Configs { get; set; } = new List<PluginConfig>();
    public ICollection<Endpoint> Endpoints { get; set; } = new List<Endpoint>();
}

public class Endpoint : GatewayModel
{
    public required Guid Id { get; set; }
    public required string Path { get; set; }
    public required Target Target { get; set; } = null!;
    public Pipe Pipe { get; set; } = null!;
    public ICollection<PluginConfig> Configs { get; set; } = new List<PluginConfig>();
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
    public required string PluginTitle { get; set; }
    public required string PluginVersion { get; set; }
    public required string ServiceTitle { get; set; }
    public required Guid PipeId { get; set; }
    public required int Order { get; set; }
    public ServiceFailurePolicies FailurePolicy { get; set; }
    // public ICollection<Endpoint>? Endpoints { get; set; }
}

public class PluginConfig : GatewayModel
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public required string Type { get; set; }
    public required string Namespace { get; set; } = "global";
    public Guid? PipeId { get; set; }
    public bool Internal { get; set; } = false;

    public Pipe? Pipe { get; set; }
}

public class Target : GatewayModel
{
    public required string Path { get; set; }
    public required string Host { get; set; }
    public required string Scheme { get; set; }
}