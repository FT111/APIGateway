using GatewayPluginContract.Entities;

namespace Supervisor.routes.Endpoints;

public static class Models
{
    public class EndpointResponse
    {
        public Guid Id { get; set; }
        public string Path { get; set; } = null!;
        public string? TargetPathPrefix { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        public Deployment Deployment { get; set; } = null!;
        public required Targets.Models.TargetResponse? Target { get; set; }
    }
    
    public class CreateEndpointRequest
    {
        public required string Path { get; init; }
        public string? TargetPathPrefix { get; init; }
        public required Guid TargetId { get; init; }
        public required Guid PipeId { get; init; }
    }
    
}

public static class Mapping
{
    public static readonly System.Linq.Expressions.Expression<System.Func<GatewayPluginContract.Entities.Endpoint, Models.EndpointResponse>> ToResponse = endpoint => new Models.EndpointResponse
    {
        Id = endpoint.Id,
        Path = endpoint.Path,
        TargetPathPrefix = endpoint.TargetPathPrefix,
        CreatedAt = endpoint.CreatedAt,
        UpdatedAt = endpoint.UpdatedAt,
        Target = endpoint.Target!=null ? new Targets.Models.TargetResponse
        {
            Id = endpoint.Target.Id,
            Url = endpoint.Target.Schema + endpoint.Target.Host + endpoint.Target.BasePath,
            Scheme = endpoint.Target.Schema,
            Host = endpoint.Target.Host,
            Path = endpoint.Target.BasePath,
            Fallback = endpoint.Target.Fallback,
            CreatedAt = endpoint.Target.CreatedAt,
            UpdatedAt = endpoint.Target.UpdatedAt
        } : null
    };
}