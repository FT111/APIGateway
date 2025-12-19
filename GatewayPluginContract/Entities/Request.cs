using GatewayPluginContract.Attributes;

namespace GatewayPluginContract.Entities;

public partial class Request
{
    public Guid Id { get; set; }
    [Queryable]
    public string? SourceAddress { get; set; }
    [Queryable]
    public virtual Target RoutedTarget { get; set; }
    public int HttpStatus { get; set; }
    public Guid RoutedTargetId { get; set; }
    public virtual Endpoint? Endpoint { get; set; }
    public Guid? EndpointId { get; set; }
    
    public Guid? InstanceId { get; set; }

    public virtual Instance Instance {
        get;
        set;
    }
    [Sortable]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public virtual ICollection<RequestGroup> Groups { get; set; } = new List<RequestGroup>();
}
