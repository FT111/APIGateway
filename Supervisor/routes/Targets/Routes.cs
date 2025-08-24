using System.Text.Json;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;

namespace Supervisor.routes.Targets;

public class Routes
{
    public Routes(WebApplication app)
    {
        var route = app.MapGroup("/targets").RequireAuthorization();

        route.MapGet("/", async (InternalTypes.Repositories.Gateway data, Utils.ResponseStructure<Models.TargetResponse> res) =>
        {
            var targets = data.Context.Set<Target>().Include(t => t.Endpoints).Select(Mapping.ToResponse);
            return Results.Json(await res.WithData(targets).WithPagination());
        });
        
        route.MapGet("/{id:guid}", async (Guid id, InternalTypes.Repositories.Gateway data) =>
        {
            var target = await data.Context.Set<Target>().Include(t => t.Endpoints).FirstOrDefaultAsync(t => t.Id == id);
            return target != null ? Results.Json(target) : Results.NotFound();
        });
        
        route.MapPost("/", async (Target target, InternalTypes.Repositories.Gateway data) =>
        {
            if (target.Id == Guid.Empty)
            {
                target.Id = Guid.NewGuid();
            }
            await data.Context.Set<Target>().AddAsync(target);
            await data.Context.SaveChangesAsync();
            return Results.Created($"/targets/{target.Id}", target);
        });
        
        route.MapPut("/{id:guid}", async (Guid id, Target target, InternalTypes.Repositories.Gateway data) =>
        {
            if (id != target.Id)
            {
                return Results.BadRequest("ID mismatch");
            }
            
            var existingTarget = await data.Context.Set<Target>().FindAsync(id);
            if (existingTarget == null)
            {
                return Results.NotFound();
            }

            data.Context.Entry(existingTarget).CurrentValues.SetValues(target);
            await data.Context.SaveChangesAsync();
            return Results.NoContent();
        });
        
    }
}