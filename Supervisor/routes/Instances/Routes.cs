using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.AspNetCore.Authorization;

namespace Supervisor.routes.Instances;

public class Routes
{
    public Routes(WebApplication app)
    {
        var route = app.MapGroup("/instances").RequireAuthorization();
        new Events.Routes(route);

        route.MapGet("/", async (HttpContext context, services.Instances.InstanceManager manager, Utils.ResponseStructure<Models.InstanceResponse> res) =>
        {
            try
            {
                var instances = manager.Instances.Values
                    .Select(i => Mapping.ToResponse.Compile()(i.Instance, i.LastSeen)).ToList();
                
                return Results.Ok(res.WithData(instances));
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message, statusCode: 500);
            }
        });
        
        route.MapDelete("/{id:guid}", async (Guid id, services.Instances.InstanceManager manager, Utils.ResponseStructure<string> res) =>
        {
            try
            {
                await manager.DeleteInstanceAsync(id);

                return Results.Ok(res.WithData($"Instance {id} removed successfully"));
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message, statusCode: 500);
            }
        });
    }
}