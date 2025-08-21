using System;
using System.Collections.Generic;
using GatewayPluginContract.Entities;

namespace GatewayPluginContract.Entities;

public partial class Pipe : Entity
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }

    public bool Global { get; set; }

    public virtual ICollection<Endpoint> Endpoints { get; set; } = new List<Endpoint>();

    public virtual ICollection<PipeService> PipeServices { get; set; } = new List<PipeService>();

    public virtual ICollection<PluginConfig> PluginConfigs { get; set; } = new List<PluginConfig>();
}
