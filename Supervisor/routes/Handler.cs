namespace Supervisor.routes;

public static class Handler
{
    public static void HandleRoutes(WebApplication app)
    {
        // Register all routes here
        new Users(app);
        new Instances(app);
    }
}