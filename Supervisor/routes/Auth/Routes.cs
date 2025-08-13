using System.IdentityModel.Tokens.Jwt;
using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.IdentityModel.Tokens;
using Supervisor.auth;

namespace Supervisor.routes.Auth;

public class Routes
{
    public Routes(WebApplication app)
    {
        var route = app.MapGroup("/auth");

        route.MapPost("/token", async (Models.TokenRequest creds, IGatewayRepositories data, AuthHandler auth) =>
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var userRepo = data.GetRepo<User>();

            var user = userRepo.QueryAsync(u => u.Username == creds.Username).Result.FirstOrDefault();
            if (user == null || !auth.VerifyPassword(creds.Password, user.Passwordhs))
            {
                return Results.Unauthorized();
            }

            var claims = new List<System.Security.Claims.Claim>
            {
                new (System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
                new (System.Security.Claims.ClaimTypes.Name, user.Username),
                new (System.Security.Claims.ClaimTypes.Role, user.Role)
            };

            var tokenSecondsExpiry = app.Configuration["Auth:Expiry"];
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new System.Security.Claims.ClaimsIdentity(claims),
                Audience = app.Configuration["Auth:Audience"] ?? throw new InvalidOperationException("Audience not configured."),
                Issuer = app.Configuration["Auth:Issuer"] ?? throw new InvalidOperationException("Issuer not configured."),

                Expires = DateTime.UtcNow.AddSeconds(
                    string.IsNullOrEmpty(tokenSecondsExpiry) ? 3600 : int.Parse(tokenSecondsExpiry)),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Convert.FromBase64String(app.Configuration["Auth:Secret"] ?? throw new InvalidOperationException("Secret key not configured."))),
                    SecurityAlgorithms.HmacSha256Signature)
            };
            
            var jwtToken = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(jwtToken);

            return Results.Ok(new
            {
                Token = tokenString,
                Expires = tokenDescriptor.Expires?.ToString("o"),
            });
        });
    }
}