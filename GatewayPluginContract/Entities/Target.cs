﻿using System;
using System.Collections.Generic;

namespace GatewayPluginContract.Entities;

public partial class Target : Entity
{
    public Guid Id { get; set; }

    public string Schema { get; set; } = null!;

    public string Host { get; set; } = null!;

    public string? BasePath { get; set; }

    public bool Fallback { get; set; }
    public virtual ICollection<Endpoint> Endpoints { get; set; } = new List<Endpoint>();
}
