using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.AspNetCore.Authorization;

namespace Supervisor.routes.Instances;

public class Routes
{
    public Routes(WebApplication app)
    {
        var route = app.MapGroup("/instances");
        new Events.Routes(route);

    }
}