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
        
        public Deployments.Models.DeploymentResponse Deployment { get; set; } = null!;
        public required Targets.Models.TargetResponse? Target { get; set; }
    }
    
    public class CreateEndpointRequest
    {
        public required string Path { get; init; }
        public string? TargetPathPrefix { get; init; }
        public Guid TargetId { get; init; }
        public required Guid PipeId { get; init; }
        public required Guid DeploymentId { get; init; }
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
        Deployment = new Deployments.Models.DeploymentResponse
        {
            Id = endpoint.Deployment.Id,
            Title = endpoint.Deployment.Title,
            CreatedAt = endpoint.Deployment.CreatedAt,
            UpdatedAt = endpoint.Deployment.UpdatedAt,
            StatusTitle = endpoint.Deployment.Status.Title,
            StatusHexColour = endpoint.Deployment.Status.HexColour
        },
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