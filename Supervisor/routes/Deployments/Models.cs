namespace Supervisor.routes.Deployments;

public static class Models
{
    public class CreateDeploymentRequest
    {
        public required string Title { get; init; }
        public required Guid PipeId { get; init; }
        public Guid? SchemaId { get; init; }
    }
    
    public class DeploymentResponse
    {
        public required Guid Id { get; init; }
        public required string Title { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime UpdatedAt { get; init; }
        
        public required string StatusTitle { get; init; }
        public required string? StatusHexColour { get; init; }
    }
    
    public class DeploymentWithSchemaAndTargetResponse : DeploymentResponse
    {
        public required Schemas.Models.SchemaWithMinifiedEndpointsResponse Schema { get; set; }
        public required Targets.Models.TargetResponse Target { get; set; }
    }
}

public static class Mapping
{
    public static readonly System.Linq.Expressions.Expression<System.Func<GatewayPluginContract.Entities.Deployment, Models.DeploymentWithSchemaAndTargetResponse>> ToResponse = deployment => new Models.DeploymentWithSchemaAndTargetResponse
    {
        Id = deployment.Id,
        Title = deployment.Title,
        CreatedAt = deployment.CreatedAt,
        UpdatedAt = deployment.UpdatedAt,
        StatusTitle = deployment.Status.Title,
        StatusHexColour = deployment.Status.HexColour,
        Schema = deployment.Schema != null ? new Schemas.Models.SchemaWithMinifiedEndpointsResponse
        {
            Id = deployment.Schema.Id,
            Title = deployment.Schema.Title,
            Description = deployment.Schema.Description,
            CreatedAt = deployment.Schema.CreatedAt,
            UpdatedAt = deployment.Schema.UpdatedAt,
            EndpointCount = deployment.Schema.Endpoints.Count
        } : null,
        Target = deployment.Target != null ? new Targets.Models.TargetResponse
        {
            Id = deployment.Target.Id,
            Url = deployment.Target.Schema + deployment.Target.Host + deployment.Target.BasePath,
            Scheme = deployment.Target.Schema,
            Host = deployment.Target.Host,
            Path = deployment.Target.BasePath,
            Fallback = deployment.Target.Fallback,
            CreatedAt = deployment.Target.CreatedAt,
            UpdatedAt = deployment.Target.UpdatedAt
        } : null
    };
}