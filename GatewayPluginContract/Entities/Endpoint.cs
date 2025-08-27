using System;
using System.Collections.Generic;
using GatewayPluginContract.Attributes;

namespace GatewayPluginContract.Entities;

public partial class Endpoint : Entity
{
    public Guid Id { get; set; }

    [Queryable]
    public string Path { get; set; } = null!;

    public string? TargetPathPrefix { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }

    public Guid? TargetId { get; set; }

    public Guid? PipeId { get; set; }
    
    public Guid? ParentId { get; set; }
    
    public virtual Endpoint? Parent { get; set; } = null;

    public virtual ICollection<Event> Events { get; set; } = new List<Event>();
    public virtual ICollection<Request> Requests { get; set; } = new List<Request>();
    
    public virtual Deployment Deployment { get; set; } = null!;

    public virtual Pipe? Pipe { get; set; } = null!;

    public virtual Target? Target { get; set; } = null!;
}
