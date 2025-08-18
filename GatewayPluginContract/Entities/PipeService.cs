namespace GatewayPluginContract.Entities;

public partial class PipeService : Entity
{
    public Guid PipeId { get; set; }

    public string PluginVersion { get; set; } = null!;

    public string PluginTitle { get; set; } = null!;

    public string ServiceTitle { get; set; } = null!;

    public long Order { get; set; }

    public virtual Pipe Pipe { get; set; } = null!;
    
    public ServiceFailurePolicies FailurePolicy { get; set; }
}
