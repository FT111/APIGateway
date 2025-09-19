using Microsoft.EntityFrameworkCore;

namespace Supervisor.routes.Deployments;

public class Routes
{
    public Routes(WebApplication app)
    {
        var group = app.MapGroup("/deployments").RequireAuthorization();
        group.MapGet("/",
            async (HttpContext context, InternalTypes.Repositories.Gateway data,
                Utils.ResponseStructure<Models.DeploymentResponse> res) =>
            {
                var deployments = data.Context.Set<GatewayPluginContract.Entities.Deployment>()
                    .Include(d => d.Status)
                    .AsNoTracking()
                    .Select(Mapping.ToResponse);
                return await res.WithData(deployments).WithPagination();
            }).WithTags("Queryable").WithName("GetDeployments");

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
    }
}