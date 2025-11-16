namespace GatewayPluginContract.Entities;

public partial class RequestGroup
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public required string Type { get; set; }
    public string? SourcePlugin { get; set; }
}