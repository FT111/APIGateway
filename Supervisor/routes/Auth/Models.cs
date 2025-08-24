namespace Supervisor.routes.Auth;

public class Models
{
    public class TokenRequest
    {
        public required string Username { get; init; }
        public required string Password { get; init; }  
        // public required List<string> Claims { get; init; }
    }
    
    public class TokenResponse
    {
        public required string Token { get; init; }
        public required string ExpiresAt { get; init; }
    }
}