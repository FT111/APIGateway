namespace Supervisor.routes.Deployments;

public static class Models
{
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