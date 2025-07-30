using System;
using System.Collections.Generic;

namespace GatewayPluginContract.Entities;

public partial class PluginData : Entity
{
    public string Namespace { get; set; } = null!;

    public string Key { get; set; } = null!;

    public string? Value { get; set; }

    public string Type { get; set; } = "string";
}
