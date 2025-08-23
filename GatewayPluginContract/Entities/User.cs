using System;

namespace GatewayPluginContract.Entities;

public partial class User : Entity
{
    public Guid Id { get; set; }
    
    public string Username { get; set; } = null!;
    
    public string Passwordhs { get; set; } = null!;
    
    public string Role { get; set; } = null!;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}