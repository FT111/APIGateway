using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.AspNetCore.Authorization;
using Supervisor.auth;

namespace Supervisor.routes;

public class Users
{
    public Users(WebApplication app)
    {
        var route = app.MapGroup("/users").RequireAuthorization();
        
        // GET /users - Get all users
        route.MapGet("/", async (InternalTypes.Repositories.Supervisor data) =>
        {
            var users = await data.GetRepo<User>().GetAllAsync();
            // Remove password hashes from response for security
            var sanitizedUsers = users.Select(u => new
            {
                u.Id,
                u.Username,
                u.Role,
                u.CreatedAt,
                u.UpdatedAt
            });
            return Results.Ok(sanitizedUsers);
        }).WithOpenApi();
        
        // GET /users/{id} - Get user by ID
        route.MapGet("/{id:guid}", async (Guid id, InternalTypes.Repositories.Supervisor data) =>
        {
            var user = await data.GetRepo<User>().GetAsync(id);
            if (user == null) return Results.NotFound();
            
            // Remove password hash from response for security
            var sanitizedUser = new
            {
                user.Id,
                user.Username,
                user.Role,
                user.CreatedAt,
                user.UpdatedAt
            };
            return Results.Ok(sanitizedUser);
        }).WithOpenApi();
        
        // POST /users - Create new user
        route.MapPost("/", async (CreateUserRequest request, InternalTypes.Repositories.Supervisor data, AuthHandler auth) =>
        {
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = request.Username,
                Passwordhs = auth.GeneratePasswordHash(request.Password),
                Role = request.Role,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            await data.GetRepo<User>().AddAsync(user);
            
            // Return user without password hash
            var sanitizedUser = new
            {
                user.Id,
                user.Username,
                user.Role,
                user.CreatedAt,
                user.UpdatedAt
            };
            return Results.Created($"/users/{user.Id}", sanitizedUser);
        }).WithOpenApi();
        
        // PUT /users/{id} - Update user
        route.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request, InternalTypes.Repositories.Supervisor data, AuthHandler auth) =>
        {
            var repo = data.GetRepo<User>();
            var user = await repo.GetAsync(id);
            
            if (user == null)
                return Results.NotFound();
                
            user.Username = request.Username ?? user.Username;
            user.Role = request.Role ?? user.Role;
            
            if (!string.IsNullOrEmpty(request.Password))
            {
                user.Passwordhs = auth.GeneratePasswordHash(request.Password);
            }
            
            user.UpdatedAt = DateTime.UtcNow;
            
            await repo.UpdateAsync(user);
            
            // Return user without password hash
            var sanitizedUser = new
            {
                user.Id,
                user.Username,
                user.Role,
                user.CreatedAt,
                user.UpdatedAt
            };
            return Results.Ok(sanitizedUser);
        }).WithOpenApi();
        
        // DELETE /users/{id} - Delete user
        route.MapDelete("/{id:guid}", async (Guid id, InternalTypes.Repositories.Supervisor data) =>
        {
            var repo = data.GetRepo<User>();
            var user = await repo.GetAsync(id);
            
            if (user == null)
                return Results.NotFound();
                
            await repo.RemoveAsync(id.ToString());
            return Results.NoContent();
        }).WithOpenApi();
    }
}

public record CreateUserRequest(
    string Username,
    string Password,
    string Role
);

public record UpdateUserRequest(
    string? Username,
    string? Password,
    string? Role
);