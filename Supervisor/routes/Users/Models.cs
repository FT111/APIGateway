namespace Supervisor.routes.Users;

public static class Models
{
    public class UserResponse
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
    
    public class CreateUserRequest
    {
        public required string Username { get; init; }
        public required string Password { get; init; }
    }
}

public static class Mapping
{
    public static readonly System.Linq.Expressions.Expression<System.Func<GatewayPluginContract.Entities.User, Models.UserResponse>> ToResponse = user => new Models.UserResponse
    {
        Id = user.Id,
        Username = user.Username,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt
    };
}