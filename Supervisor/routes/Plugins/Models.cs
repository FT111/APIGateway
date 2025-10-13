using System.Linq.Expressions;
using GatewayPluginContract.Attributes;
using GatewayPluginContract.Entities;

namespace Supervisor.routes.Plugins;

public static class Models
{
    public class PluginResponse
    {
        [Queryable]
        public required string Title { get; init; }
        [Queryable]
        public required string Version { get; init; }
        [Queryable]
        public string Identifier => $"{Title}/{Version}";
    }
}

public static class Mapping
{
    public static readonly Expression<Func<Plugin, Models.PluginResponse>> ToResponse = plugin =>
        new Models.PluginResponse
        {
            Title = plugin.Title,
            Version = plugin.Version
        };

}