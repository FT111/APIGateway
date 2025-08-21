using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;

namespace Supervisor.routes.Pipes;

public class Routes
{
    public Routes(WebApplication app)
    {
        var route = app.MapGroup("/pipes").RequireAuthorization();
        
        route.MapGet("/", async (InternalTypes.Repositories.Supervisor data, Utils.Paginator<Models.PipeResponse> paginator) =>
        {
            var pipes = data.Context.Set<Pipe>()
                .Include(p => p.Endpoints)
                .AsNoTracking()
                .Select(Mapping.PipeToResponse);
            var paginatedPipes = paginator.Apply(pipes).ToList();
            
            return Results.Ok(paginatedPipes);
        });
        
        route.MapGet("/{id:guid}", async (Guid id, InternalTypes.Repositories.Supervisor data) =>
        {
            var pipe = await data.GetRepo<Pipe>().GetAsync(id);
            return pipe != null ? Results.Ok(pipe) : Results.NotFound();
        });
        
        route.MapPost("/", async (Pipe pipe, InternalTypes.Repositories.Supervisor data) =>
        {
            if (pipe.Id == Guid.Empty)
            {
                pipe.Id = Guid.NewGuid();
            }
            await data.GetRepo<Pipe>().AddAsync(pipe);
            return Results.Created($"/pipes/{pipe.Id}", pipe);
        });
        
        route.MapPut("/{id:guid}", async (Guid id, Pipe pipe, InternalTypes.Repositories.Supervisor data) =>
        {
            if (id != pipe.Id)
            {
                return Results.BadRequest("ID mismatch");
            }
            
            var existingPipe = await data.GetRepo<Pipe>().GetAsync(id);
            if (existingPipe == null)
            {
                return Results.NotFound();
            }

            await data.GetRepo<Pipe>().UpdateAsync(pipe);
            return Results.NoContent();
        });
        
    }
}