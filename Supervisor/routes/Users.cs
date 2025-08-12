using GatewayPluginContract;
using GatewayPluginContract.Entities;

namespace Supervisor.routes;

public class Users
{
    public Users(WebApplication app)
    {
        var route = app.MapGroup("/users");
        
        route.MapGet("/", async (IGatewayRepositories data) =>
        {
            var users = await data.GetRepo<User>().GetAllAsync();
            return Results.Ok(users);
        });
    }
}