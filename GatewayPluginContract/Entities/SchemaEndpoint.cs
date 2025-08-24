namespace GatewayPluginContract.Entities;

public class SchemaEndpoint
{
    public Guid Id { get; set; }
    public string Path { get; set; } = null!;
    public Guid SchemaId { get; set; }
    
    public virtual Schema Schema { get; set; } = null!;
}