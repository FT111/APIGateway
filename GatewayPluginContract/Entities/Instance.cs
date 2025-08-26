namespace GatewayPluginContract.Entities;

public class Instance
{
    public Guid Id { get; set; }
    public byte[] PublicKey { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; init; }
}