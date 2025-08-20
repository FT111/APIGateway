using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.AspNetCore.Authorization;

namespace Supervisor.routes;

public class PipeServices
{
    public PipeServices(WebApplication app)
    {
        var route = app.MapGroup("/pipe-services").RequireAuthorization();
        
        // GET /pipe-services - Get all pipe services
        route.MapGet("/", async (InternalTypes.Repositories.Supervisor data) =>
        {
            var pipeServices = await data.GetRepo<PipeService>().GetAllAsync();
            return Results.Ok(pipeServices);
        }).WithOpenApi();
        
        // GET /pipe-services/pipe/{pipeId} - Get pipe services for a specific pipe
        route.MapGet("/pipe/{pipeId:guid}", async (Guid pipeId, InternalTypes.Repositories.Supervisor data) =>
        {
            var repo = data.GetRepo<PipeService>();
            var pipeServices = await repo.QueryAsync(ps => ps.PipeId == pipeId);
            return Results.Ok(pipeServices.OrderBy(ps => ps.Order));
        }).WithOpenApi();
        
        // POST /pipe-services - Create new pipe service
        route.MapPost("/", async (CreatePipeServiceRequest request, InternalTypes.Repositories.Supervisor data) =>
        {
            var pipeService = new PipeService
            {
                PipeId = request.PipeId,
                PluginVersion = request.PluginVersion,
                PluginTitle = request.PluginTitle,
                ServiceTitle = request.ServiceTitle,
                Order = request.Order,
                FailurePolicy = request.FailurePolicy
            };
            
            await data.GetRepo<PipeService>().AddAsync(pipeService);
            return Results.Created($"/pipe-services/pipe/{pipeService.PipeId}", pipeService);
        }).WithOpenApi();
        
        // PUT /pipe-services/{pipeId}/{pluginTitle}/{serviceTitle} - Update pipe service
        route.MapPut("/{pipeId:guid}/{pluginTitle}/{serviceTitle}", async (
            Guid pipeId, 
            string pluginTitle, 
            string serviceTitle, 
            UpdatePipeServiceRequest request, 
            InternalTypes.Repositories.Supervisor data) =>
        {
            var repo = data.GetRepo<PipeService>();
            var pipeServiceList = await repo.QueryAsync(ps => 
                ps.PipeId == pipeId && 
                ps.PluginTitle == pluginTitle && 
                ps.ServiceTitle == serviceTitle);
            var pipeService = pipeServiceList.FirstOrDefault();
            
            if (pipeService == null)
                return Results.NotFound();
                
            pipeService.PluginVersion = request.PluginVersion ?? pipeService.PluginVersion;
            pipeService.Order = request.Order ?? pipeService.Order;
            pipeService.FailurePolicy = request.FailurePolicy ?? pipeService.FailurePolicy;
            
            await repo.UpdateAsync(pipeService);
            return Results.Ok(pipeService);
        }).WithOpenApi();
        
        // DELETE /pipe-services/{pipeId}/{pluginTitle}/{serviceTitle} - Delete pipe service
        route.MapDelete("/{pipeId:guid}/{pluginTitle}/{serviceTitle}", async (
            Guid pipeId, 
            string pluginTitle, 
            string serviceTitle, 
            InternalTypes.Repositories.Supervisor data) =>
        {
            var repo = data.GetRepo<PipeService>();
            var pipeServiceList = await repo.QueryAsync(ps => 
                ps.PipeId == pipeId && 
                ps.PluginTitle == pluginTitle && 
                ps.ServiceTitle == serviceTitle);
            var pipeService = pipeServiceList.FirstOrDefault();
            
            if (pipeService == null)
                return Results.NotFound();
                
            await repo.RemoveAsync($"{pipeId}-{pluginTitle}-{serviceTitle}");
            return Results.NoContent();
        }).WithOpenApi();
    }
}

public record CreatePipeServiceRequest(
    Guid PipeId,
    string PluginVersion,
    string PluginTitle,
    string ServiceTitle,
    long Order,
    ServiceFailurePolicies FailurePolicy = ServiceFailurePolicies.Ignore
);

public record UpdatePipeServiceRequest(
    string? PluginVersion,
    long? Order,
    ServiceFailurePolicies? FailurePolicy
);