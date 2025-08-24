namespace GatewayPluginContract.Entities;

public class Schema
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public virtual ICollection<Deployment> Deployments { get; set; } = new List<Deployment>();
    public virtual ICollection<SchemaEndpoint> Endpoints { get; set; } = new List<SchemaEndpoint>();
}