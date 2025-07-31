using System;
using System.Collections.Generic;

namespace GatewayPluginContract.Entities;

public partial class Endpoint : Entity
{
    public Guid Id { get; set; }

    public string Path { get; set; } = null!;

    public string? TargetPathPrefix { get; set; }

    public Guid TargetId { get; set; }

    public Guid PipeId { get; set; }

    public virtual ICollection<Event> Events { get; set; } = new List<Event>();

    public virtual Pipe Pipe { get; set; } = null!;
    public virtual Target Target { get; set; } = null!;
}
