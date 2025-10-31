namespace GatewayPluginContract.Entities;

public partial class PluginConfig : Entity
{
    public string Key { get; set; } = null!;

    public string? Value { get; set; }

    public string Namespace { get; set; } = null!;

    public required Guid? PipeId { get; set; } = null;
    
    public string Type { get; set; }

    public bool Internal { get; set; }

    // public virtual Pipe? Pipe { get; set; } = null!;
}
