using GatewayPluginContract.Entities;

namespace Supervisor.routes;

public static class Handler
{
    public static void HandleRoutes(WebApplication app)
    {
        // Register all routes here
        new Users.Routes(app);
        new Instances(app);
        new Auth.Routes(app);
        new Targets.Routes(app);
        new Pipes.Routes(app);
    }
}