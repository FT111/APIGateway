using System;
using System.Collections.Generic;

namespace GatewayPluginContract.Entities;

public partial class User : Entity
{
    public Guid Id { get; set; }

    public string Username { get; set; } = null!;

    public string Passwordhs { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
