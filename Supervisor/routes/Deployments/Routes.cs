using Microsoft.EntityFrameworkCore;

namespace Supervisor.routes.Deployments;

public class Routes
{
    public Routes(WebApplication app)
    {
        var group = app.MapGroup("/deployments").RequireAuthorization();
        group.MapGet("/",
            async (HttpContext context, InternalTypes.Repositories.Gateway data,
                Utils.ResponseStructure<Models.DeploymentWithSchemaAndTargetResponse> res) =>
            {
                var deployments = data.Context.Set<GatewayPluginContract.Entities.Deployment>()
                    .Include(d => d.Status)
                    .Include(d => d.Schema)
                    .Include(d => d.Target)
                    .AsNoTracking()
                    .Select(Mapping.ToResponse);
                return await res.WithData(deployments).WithPagination();
            }).WithTags("Queryable").WithName("GetDeployments");
        
        group.MapGet("/{id:guid}",
            async (Guid id, InternalTypes.Repositories.Gateway data,
                Utils.ResponseStructure<Models.DeploymentWithEndpointsResponse> res) =>
            {
                var deployment = await data.Context.Set<GatewayPluginContract.Entities.Deployment>()
                    .Include(d => d.Status)
                    .Include(d => d.Schema)
                    .Include(d => d.Schema)
                    .Include(d => d.Endpoints)
                    .Include(e => e.Target)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == id);
                if (deployment == null)
                {
                    return Results.NotFound();
                }
                var mappedResponse = Mapping.ToWithEndpointsResponse.Compile()(deployment);
                return Results.Ok(res.WithData(mappedResponse));
            }).WithTags("Queryable").WithName("GetDeploymentById");

        group.MapPost("/",
            async (HttpContext context, InternalTypes.Repositories.Gateway data,
                Models.CreateDeploymentRequest deployment, Utils.ResponseStructure<Models.DeploymentResponse> res) =>
            {
                var deploymentEntity = new GatewayPluginContract.Entities.Deployment
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    StatusId = Guid.Parse("2c7292f8-ab97-4cf6-b4e2-12256742467e")
                };
                await data.GetRepo<GatewayPluginContract.Entities.Deployment>().AddAsync(deploymentEntity);
                var mappedResponse = Mapping.ToResponse.Compile()(deploymentEntity);
                return Results.Created($"/deployments/{deploymentEntity.Id}", res.WithData(mappedResponse));
            }).WithName("CreateDeployment");

        group.MapPut("/{id:guid}",
            async (Guid id, GatewayPluginContract.Entities.Deployment deployment,
                InternalTypes.Repositories.Gateway data) =>
            {
                if (id != deployment.Id)
                {
                    return Results.BadRequest("ID mismatch");
                }

                var existingDeployment = await data.GetRepo<GatewayPluginContract.Entities.Deployment>().GetAsync(id);
                if (existingDeployment == null)
                {
                    return Results.NotFound();
                }

                data.Context.Entry(existingDeployment).CurrentValues.SetValues(deployment);
                return Results.NoContent();
            }).WithName("UpdateDeployment");
        
        group.MapDelete("/{id:guid}", async (Guid id, InternalTypes.Repositories.Gateway data, Utils.ResponseStructure<string> res) =>
        {
            try
            {
                var existingDeployment = await data.GetRepo<GatewayPluginContract.Entities.Deployment>().GetAsync(id);
                if (existingDeployment == null)
                {
                    return Results.NotFound();
                }
                
                await data.GetRepo<GatewayPluginContract.Entities.Deployment>().RemoveAsync(existingDeployment.Id.ToString());
                return Results.Ok(res.WithData($"Deployment {id} removed successfully"));
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message, statusCode: 500);
            }
        }).WithName("DeleteDeployment");
    }
}