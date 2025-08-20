namespace Supervisor.routes;

public static class Handler
{
    public static void HandleRoutes(WebApplication app)
    {
        // Register all routes here
        new Users(app);
        new Instances(app);
        new Auth.Routes(app);
        
        // CRUD endpoints for main entities
        new Endpoints(app);
        new Targets(app);
        new Pipes(app);
        new PluginDataRoutes(app);
        new Events(app);
        new PipeServices(app);
        new PluginConfigs(app);
        new Requests(app);
    }
}