using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.AspNetCore.Authorization;

namespace Supervisor.routes;

public class Targets
{
    public Targets(WebApplication app)
    {
        var route = app.MapGroup("/targets").RequireAuthorization();
        
        // GET /targets - Get all targets
        route.MapGet("/", async (InternalTypes.Repositories.Supervisor data) =>
        {
            var targets = await data.GetRepo<Target>().GetAllAsync();
            return Results.Ok(targets);
        }).WithOpenApi();
        
        // GET /targets/{id} - Get target by ID
        route.MapGet("/{id:guid}", async (Guid id, InternalTypes.Repositories.Supervisor data) =>
        {
            var target = await data.GetRepo<Target>().GetAsync(id);
            return target != null ? Results.Ok(target) : Results.NotFound();
        }).WithOpenApi();
        
        // POST /targets - Create new target
        route.MapPost("/", async (CreateTargetRequest request, InternalTypes.Repositories.Supervisor data) =>
        {
            var target = new Target
            {
                Id = Guid.NewGuid(),
                Schema = request.Schema,
                Host = request.Host,
                BasePath = request.BasePath,
                Fallback = request.Fallback
            };
            
            await data.GetRepo<Target>().AddAsync(target);
            return Results.Created($"/targets/{target.Id}", target);
        }).WithOpenApi();
        
        // PUT /targets/{id} - Update target
        route.MapPut("/{id:guid}", async (Guid id, UpdateTargetRequest request, InternalTypes.Repositories.Supervisor data) =>
        {
            var repo = data.GetRepo<Target>();
            var target = await repo.GetAsync(id);
            
            if (target == null)
                return Results.NotFound();
                
            target.Schema = request.Schema ?? target.Schema;
            target.Host = request.Host ?? target.Host;
            target.BasePath = request.BasePath ?? target.BasePath;
            target.Fallback = request.Fallback ?? target.Fallback;
            
            await repo.UpdateAsync(target);
            return Results.Ok(target);
        }).WithOpenApi();
        
        // DELETE /targets/{id} - Delete target
        route.MapDelete("/{id:guid}", async (Guid id, InternalTypes.Repositories.Supervisor data) =>
        {
            var repo = data.GetRepo<Target>();
            var target = await repo.GetAsync(id);
            
            if (target == null)
                return Results.NotFound();
                
            await repo.RemoveAsync(id.ToString());
            return Results.NoContent();
        }).WithOpenApi();
    }
}

public record CreateTargetRequest(
    string Schema,
    string Host,
    string? BasePath,
    bool Fallback = false
);

public record UpdateTargetRequest(
    string? Schema,
    string? Host,
    string? BasePath,
    bool? Fallback
);