using Microsoft.EntityFrameworkCore;
using Endpoint = GatewayPluginContract.Entities.Endpoint;
namespace Supervisor.routes.Endpoints;

public class Routes
{
    public Routes(WebApplication app)
    {
        var group = app.MapGroup("/endpoints").RequireAuthorization();
        group.MapGet("/", async (HttpContext context, InternalTypes.Repositories.Gateway data, Utils.ResponseStructure<Models.EndpointResponse> res) =>
        {
            var endpoints = data.Context.Set<Endpoint>()
                .Include(e => e.Target)
                .Include(e => e.Deployment)
                .Include(e => e.Deployment.Status)
                .AsNoTracking()
                .Select(Mapping.ToResponse); 
            return await res.WithData(endpoints).WithPagination();
        }).WithTags("Queryable").WithName("GetEndpoints");

        group.MapGet("/{id:guid}",
              (Guid id, InternalTypes.Repositories.Gateway data,
                Utils.ResponseStructure<Models.EndpointResponse> res) =>
            {
                Models.EndpointResponse? endpoint = data.Context.Set<Endpoint>()
                    .Include(e => e.Target)
                    .Include(e => e.Deployment)
                    .Include(e => e.Deployment.Status)
                    .AsNoTracking()
                    .Select(Mapping.ToResponse)
                    .FirstOrDefault(e => e.Id == id);
                
                if (endpoint == null) return Results.NotFound();
                return Results.Ok(res.WithData(endpoint));
            }).WithName("GetEndpoint");

        group.MapPost("/", async (HttpContext context, InternalTypes.Repositories.Gateway data, Models.CreateEndpointRequest endpoint, Utils.ResponseStructure<Models.EndpointResponse> res) =>
        {
            // Validate endpoint is linked to a valid deployment
            var linkedDeployment = await data.Context.Set<GatewayPluginContract.Entities.Deployment>()
                .FirstOrDefaultAsync(d => d.Id == endpoint.DeploymentId);
            if (linkedDeployment == null) return Results.BadRequest("Invalid DeploymentId");
            
            var endpointEntity = new Endpoint
            {
                Id = Guid.NewGuid(),
                Path = endpoint.Path,
                TargetPathPrefix = endpoint.TargetPathPrefix,
                TargetId = endpoint.TargetId,
                Deployment = linkedDeployment,
                PipeId = endpoint.PipeId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await data.GetRepo<Endpoint>().AddAsync(endpointEntity);
            var mappedResponse = Mapping.ToResponse.Compile()(endpointEntity);
            return Results.Created($"/endpoints/{endpointEntity.Id}", res.WithData(mappedResponse));
        }).WithName("CreateEndpoint");
        
        group.MapPut("/{id:guid}", async (Guid id, Endpoint endpoint, InternalTypes.Repositories.Gateway data) =>
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
        
        group.MapDelete("/{id:guid}", async (Guid id, InternalTypes.Repositories.Gateway data) =>
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