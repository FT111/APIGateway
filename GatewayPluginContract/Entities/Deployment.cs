namespace GatewayPluginContract.Entities;

public class Deployment
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public Guid TargetId { get; set; }
    public Guid SchemaId { get; set; }
    
    public virtual Target Target { get; set; } = null!;
    public virtual Schema Schema { get; set; } = null!;
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}