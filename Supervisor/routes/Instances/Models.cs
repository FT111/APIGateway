using GatewayPluginContract.Attributes;

namespace Supervisor.routes.Instances;

public static class Models
{
    public class InstanceResponse
    {
        [Queryable]
        public required Guid Id { get; set; }
        [Queryable]
        public required string Status { get; set; }
        [Sortable]
        public DateTime CreatedAt { get; set; }
        [Sortable]
        public DateTime LastSeenAt { get; set; }
    }
}

public static class Mapping
{
    public static readonly System.Linq.Expressions.Expression<System.Func<GatewayPluginContract.Entities.Instance, DateTime, Models.InstanceResponse>> ToResponse = (instance, lastSeen) => new Models.InstanceResponse
    {
        Id = instance.Id,
        Status = instance.Status.ToString(),
        CreatedAt = instance.CreatedAt,
        LastSeenAt = lastSeen
    };
    
}