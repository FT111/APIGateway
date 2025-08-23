using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;
using Supervisor.auth;

namespace Supervisor.routes.Users;

public class Routes
{
    public Routes(WebApplication app)
    {
        var route = app.MapGroup("/users").RequireAuthorization();
        
        route.MapGet("/", async (InternalTypes.Repositories.Supervisor data, Utils.ResponseStructure<Models.UserResponse> res) =>
        {
            var users = data.Context.Set<User>()
                .AsNoTracking()
                .Select(Mapping.ToResponse);
            var result = await res.WithData(users).WithPagination();
            
            return Results.Ok(result);
        });
        route.MapGet("/{id:guid}", async (Guid id, InternalTypes.Repositories.Supervisor data, Utils.ResponseStructure<Models.UserResponse> res) =>
        {
            var user = await data.GetRepo<User>().GetAsync(id);
            var mappedUser = user != null ? Mapping.ToResponse.Compile()(user) : null;
            return mappedUser != null ? Results.Ok(res.WithData(mappedUser)) : Results.NotFound();
        });
        route.MapPost("/", async (Models.CreateUserRequest user, InternalTypes.Repositories.Supervisor data, Utils.ResponseStructure<Models.UserResponse> res, AuthHandler auth) =>
        {
            var existingUser = await data.Context.Set<User>().FirstOrDefaultAsync(u => u.Username == user.Username);
            if (existingUser != null)
            {
                return Results.Conflict("Username already exists");
            }
            
            var hashedPassword = auth.GeneratePasswordHash(user.Password);
            var newUser = new User
            {
                Id = Guid.NewGuid(),
                Username = user.Username,
                Passwordhs = hashedPassword,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            var resUser = Mapping.ToResponse.Compile()(newUser);
            await data.GetRepo<User>().AddAsync(newUser);
            return Results.Created($"/users/{resUser.Id}", res.WithData(resUser));
        });
        route.MapPut("/{id:guid}", async (Guid id, User user, InternalTypes.Repositories.Supervisor data) =>
        {
            if (id != user.Id)
            {
                return Results.BadRequest("ID mismatch");
            }
            
            var existingUser = await data.GetRepo<User>().GetAsync(id);
            if (existingUser == null)
            {
                return Results.NotFound();
            }

            await data.GetRepo<User>().UpdateAsync(user);
            return Results.NoContent();
        });
        
        route.MapGet("/me", async (System.Security.Claims.ClaimsPrincipal user, InternalTypes.Repositories.Supervisor data, Utils.ResponseStructure<Models.UserResponse> res) =>
        {
            var userIdClaim = user.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Results.Unauthorized();
            }

            var existingUser = await data.GetRepo<User>().GetAsync(userId);
            if (existingUser == null)
            {
                return Results.NotFound();
            }

            var resUser = Mapping.ToResponse.Compile()(existingUser);
            return Results.Ok(res.WithData(resUser));
        });
    }
    
}