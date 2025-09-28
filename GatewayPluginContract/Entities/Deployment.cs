using GatewayPluginContract.Attributes;

namespace GatewayPluginContract.Entities;

public class Deployment
{
    [Queryable]
    public Guid Id { get; set; }
    [Sortable]
    [Queryable]
    public string Title { get; set; } = null!;
    [Queryable]
    public Guid TargetId { get; set; }
    [Queryable]
    public Guid? SchemaId { get; set; } = null;
    [Queryable]
    public Guid StatusId { get; set; }
    
    public virtual DeploymentStatus Status { get; set; } = null!;
    
    public virtual Target Target { get; set; } = null!;
    public virtual Schema Schema { get; set; } = null!;
    public virtual ICollection<Endpoint> Endpoints { get; set; } = new List<Endpoint>();
    
    [Sortable]
    public DateTime CreatedAt { get; set; }
    [Sortable]
    public DateTime UpdatedAt { get; set; }
}