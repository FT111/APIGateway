namespace GatewayPluginContract.Entities;

public class Deployment
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public Guid TargetId { get; set; }
    public Guid? SchemaId { get; set; } = null;
    public Guid StatusId { get; set; }
    
    public virtual DeploymentStatus Status { get; set; } = null!;
    
    public virtual Target Target { get; set; } = null!;
    public virtual Schema Schema { get; set; } = null!;
    public virtual ICollection<Endpoint> Endpoints { get; set; } = new List<Endpoint>();
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}