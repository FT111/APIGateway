using GatewayPluginContract.Attributes;

namespace Supervisor.routes.Targets;

public static class Models
{
    public class TargetResponse
    {
        [Queryable]
        public Guid Id { get; set; }
        
        [Queryable]
        [Sortable]
        public string Url { get; set; } = null!;
        public string Scheme { get; set; } = null!;
        public string Host { get; set; } = null!;
        public string? Path { get; set; } = null!;
        public bool Fallback { get; set; }
        [Sortable]
        public DateTime CreatedAt { get; set; }
        [Sortable]
        public DateTime UpdatedAt { get; set; }
    }
    
    public class CreateTargetRequest
    {
        public required string Name { get; init; }
        public required string Scheme { get; init; }
        public required string Host { get; init; }
        public string? Path { get; init; }
        public bool Fallback { get; init; } = false;
    }
}

public static class Mapping
{
    public static readonly System.Linq.Expressions.Expression<System.Func<GatewayPluginContract.Entities.Target, Models.TargetResponse>> ToResponse = target => new Models.TargetResponse
    {
        Id = target.Id,
        Url = target.Schema + target.Host + target.BasePath,
        Scheme = target.Schema,
        Host = target.Host,
        Path = target.BasePath,
        Fallback = target.Fallback,
        CreatedAt = target.CreatedAt,
        UpdatedAt = target.UpdatedAt
    };
}