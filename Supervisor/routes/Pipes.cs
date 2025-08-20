using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.AspNetCore.Authorization;

namespace Supervisor.routes;

public class Pipes
{
    public Pipes(WebApplication app)
    {
        var route = app.MapGroup("/pipes").RequireAuthorization();
        
        // GET /pipes - Get all pipes
        route.MapGet("/", async (InternalTypes.Repositories.Supervisor data) =>
        {
            var pipes = await data.GetRepo<Pipe>().GetAllAsync();
            return Results.Ok(pipes);
        }).WithOpenApi();
        
        // GET /pipes/{id} - Get pipe by ID
        route.MapGet("/{id:guid}", async (Guid id, InternalTypes.Repositories.Supervisor data) =>
        {
            var pipe = await data.GetRepo<Pipe>().GetAsync(id);
            return pipe != null ? Results.Ok(pipe) : Results.NotFound();
        }).WithOpenApi();
        
        // POST /pipes - Create new pipe
        route.MapPost("/", async (CreatePipeRequest request, InternalTypes.Repositories.Supervisor data) =>
        {
            var pipe = new Pipe
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Global = request.Global
            };
            
            await data.GetRepo<Pipe>().AddAsync(pipe);
            return Results.Created($"/pipes/{pipe.Id}", pipe);
        }).WithOpenApi();
        
        // PUT /pipes/{id} - Update pipe
        route.MapPut("/{id:guid}", async (Guid id, UpdatePipeRequest request, InternalTypes.Repositories.Supervisor data) =>
        {
            var repo = data.GetRepo<Pipe>();
            var pipe = await repo.GetAsync(id);
            
            if (pipe == null)
                return Results.NotFound();
                
            pipe.Global = request.Global ?? pipe.Global;
            pipe.UpdatedAt = DateTime.UtcNow;
            
            await repo.UpdateAsync(pipe);
            return Results.Ok(pipe);
        }).WithOpenApi();
        
        // DELETE /pipes/{id} - Delete pipe
        route.MapDelete("/{id:guid}", async (Guid id, InternalTypes.Repositories.Supervisor data) =>
        {
            var repo = data.GetRepo<Pipe>();
            var pipe = await repo.GetAsync(id);
            
            if (pipe == null)
                return Results.NotFound();
                
            await repo.RemoveAsync(id.ToString());
            return Results.NoContent();
        }).WithOpenApi();
    }
}

public record CreatePipeRequest(
    bool Global = false
);

public record UpdatePipeRequest(
    bool? Global
);