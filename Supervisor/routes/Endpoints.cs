using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.AspNetCore.Authorization;

namespace Supervisor.routes;

public class Endpoints
{
    public Endpoints(WebApplication app)
    {
        var route = app.MapGroup("/endpoints").RequireAuthorization();
        
        // GET /endpoints - Get all endpoints
        route.MapGet("/", async (InternalTypes.Repositories.Supervisor data) =>
        {
            var endpoints = await data.GetRepo<GatewayPluginContract.Entities.Endpoint>().GetAllAsync();
            return Results.Ok(endpoints);
        }).WithOpenApi();
        
        // GET /endpoints/{id} - Get endpoint by ID
        route.MapGet("/{id:guid}", async (Guid id, InternalTypes.Repositories.Supervisor data) =>
        {
            var endpoint = await data.GetRepo<GatewayPluginContract.Entities.Endpoint>().GetAsync(id);
            return endpoint != null ? Results.Ok(endpoint) : Results.NotFound();
        }).WithOpenApi();
        
        // POST /endpoints - Create new endpoint
        route.MapPost("/", async (CreateEndpointRequest request, InternalTypes.Repositories.Supervisor data) =>
        {
            var endpoint = new GatewayPluginContract.Entities.Endpoint
            {
                Id = Guid.NewGuid(),
                Path = request.Path,
                TargetPathPrefix = request.TargetPathPrefix,
                TargetId = request.TargetId,
                PipeId = request.PipeId
            };
            
            await data.GetRepo<GatewayPluginContract.Entities.Endpoint>().AddAsync(endpoint);
            return Results.Created($"/endpoints/{endpoint.Id}", endpoint);
        }).WithOpenApi();
        
        // PUT /endpoints/{id} - Update endpoint
        route.MapPut("/{id:guid}", async (Guid id, UpdateEndpointRequest request, InternalTypes.Repositories.Supervisor data) =>
        {
            var repo = data.GetRepo<GatewayPluginContract.Entities.Endpoint>();
            var endpoint = await repo.GetAsync(id);
            
            if (endpoint == null)
                return Results.NotFound();
                
            endpoint.Path = request.Path ?? endpoint.Path;
            endpoint.TargetPathPrefix = request.TargetPathPrefix ?? endpoint.TargetPathPrefix;
            endpoint.TargetId = request.TargetId ?? endpoint.TargetId;
            endpoint.PipeId = request.PipeId ?? endpoint.PipeId;
            
            await repo.UpdateAsync(endpoint);
            return Results.Ok(endpoint);
        }).WithOpenApi();
        
        // DELETE /endpoints/{id} - Delete endpoint
        route.MapDelete("/{id:guid}", async (Guid id, InternalTypes.Repositories.Supervisor data) =>
        {
            var repo = data.GetRepo<GatewayPluginContract.Entities.Endpoint>();
            var endpoint = await repo.GetAsync(id);
            
            if (endpoint == null)
                return Results.NotFound();
                
            await repo.RemoveAsync(id.ToString());
            return Results.NoContent();
        }).WithOpenApi();
    }
}

public record CreateEndpointRequest(
    string Path,
    string? TargetPathPrefix,
    Guid TargetId,
    Guid PipeId
);

public record UpdateEndpointRequest(
    string? Path,
    string? TargetPathPrefix,
    Guid? TargetId,
    Guid? PipeId
);