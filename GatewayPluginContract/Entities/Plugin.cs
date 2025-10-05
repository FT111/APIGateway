namespace GatewayPluginContract.Entities;

public partial class Plugin : Entity
{
    public string Title { get; set; } = null!;
    public string Version { get; set; } = null!;
}