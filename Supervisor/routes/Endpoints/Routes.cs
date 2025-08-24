using Microsoft.EntityFrameworkCore;
using Endpoint = GatewayPluginContract.Entities.Endpoint;
namespace Supervisor.routes.Endpoints;

public class Routes
{
    public Routes(WebApplication app)
    {
        app.MapGet("/endpoints", async (HttpContext context, InternalTypes.Repositories.Gateway data, Utils.ResponseStructure<Models.EndpointResponse> res) =>
        {
            var endpoints = data.Context.Set<Endpoint>()
                .Include(e => e.Target)
                .AsNoTracking()
                .Select(Mapping.ToResponse); 
            return await res.WithData(endpoints).WithPagination();
        }).WithTags("Queryable").WithName("GetEndpoints");

        app.MapPost("/endpoints", async (HttpContext context, InternalTypes.Repositories.Gateway data, Models.CreateEndpointRequest endpoint, Utils.ResponseStructure<Models.EndpointResponse> res) =>
        {
            var endpointEntity = new Endpoint
            {
                Id = Guid.NewGuid(),
                Path = endpoint.Path,
                TargetPathPrefix = endpoint.TargetPathPrefix,
                TargetId = endpoint.TargetId,
                PipeId = endpoint.PipeId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await data.GetRepo<Endpoint>().AddAsync(endpointEntity);
            var mappedResponse = Mapping.ToResponse.Compile()(endpointEntity);
            return Results.Created($"/endpoints/{endpointEntity.Id}", res.WithData(mappedResponse));
        }).WithName("CreateEndpoint");
        
        app.MapPut("/endpoints/{id:guid}", async (Guid id, Endpoint endpoint, InternalTypes.Repositories.Gateway data) =>
        {
            if (id != endpoint.Id)
            {
                return Results.BadRequest("ID mismatch");
            }
            
            var existingEndpoint = await data.GetRepo<Endpoint>().GetAsync(id);
            if (existingEndpoint == null)
            {
                return Results.NotFound();
            }

            data.Context.Entry(existingEndpoint).CurrentValues.SetValues(endpoint);
            return Results.NoContent();
        }).WithName("UpdateEndpoint");
        
        app.MapDelete("/endpoints/{id:guid}", async (Guid id, InternalTypes.Repositories.Gateway data) =>
        {
            var existingEndpoint = await data.GetRepo<Endpoint>().GetAsync(id);
            if (existingEndpoint == null)
            {
                return Results.NotFound();
            }

            await data.Context.Set<Endpoint>().Where(e => e.Id == id).ExecuteDeleteAsync();
            return Results.NoContent();
        }).WithName("DeleteEndpoint");
        
    }
}