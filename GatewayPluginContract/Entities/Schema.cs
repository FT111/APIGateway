using GatewayPluginContract.Attributes;

namespace GatewayPluginContract.Entities;

public class Schema
{
    [Queryable]
    public Guid Id { get; set; }
    [Queryable]
    [Sortable]
    public string Title { get; set; } = null!;
    [Queryable]
    public string Description { get; set; } = null!;
    [Sortable]
    public DateTime CreatedAt { get; set; }
    [Sortable]
    public DateTime UpdatedAt { get; set; }
    
    public virtual ICollection<Deployment> Deployments { get; set; } = new List<Deployment>();
    public virtual ICollection<SchemaEndpoint> Endpoints { get; set; } = new List<SchemaEndpoint>();
}