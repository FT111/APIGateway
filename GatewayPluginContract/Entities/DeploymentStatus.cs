namespace GatewayPluginContract.Entities;

public class DeploymentStatus
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string? HexColour { get; set; } = null;
    
    public virtual ICollection<Deployment> Deployments { get; set; } = new List<Deployment>();
}