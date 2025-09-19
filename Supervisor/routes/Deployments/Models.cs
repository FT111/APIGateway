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
        public string? StatusHexColour { get; init; }
    }
}

public static class Mapping
{
    public static readonly System.Linq.Expressions.Expression<System.Func<GatewayPluginContract.Entities.Deployment, Models.DeploymentResponse>> ToResponse = deployment => new Models.DeploymentResponse
    {
        Id = deployment.Id,
        Title = deployment.Title,
        CreatedAt = deployment.CreatedAt,
        UpdatedAt = deployment.UpdatedAt,
        StatusTitle = deployment.Status.Title,
        StatusHexColour = deployment.Status.HexColour
    };
}