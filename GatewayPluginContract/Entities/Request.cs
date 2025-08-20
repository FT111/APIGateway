namespace GatewayPluginContract.Entities;

public partial class Request : Entity
{
    public Guid Id { get; set; }
    public string? SourceAddress { get; set; }
    public virtual Endpoint? Endpoint { get; set; }
    public Guid? EndpointId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
