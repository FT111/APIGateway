using BCrypt.Net;

namespace Supervisor.auth;

public class AuthHandler(IConfiguration conf)
{
    private readonly string _secret = conf["Auth:Secret"] ?? throw new ArgumentNullException("Auth:Secret");
    
    public string GeneratePasswordHash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(10));
    }
    
    public bool VerifyPassword(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
        {
            throw new ArgumentException("Password, salt, and hash must not be null or empty.");
        }

        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}