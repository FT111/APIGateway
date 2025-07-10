using System.Net;
using Gateway.services;
using GatewayPluginContract;

namespace Gateway.routes;


public static class ApiRoutes
{
    private static Dictionary<string, Dictionary<string, string>> _getBaseConfig()
    {
        using StreamReader reader = new StreamReader("settings.json");
        var json = reader.ReadToEnd();
        return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json) ?? new Dictionary<string, Dictionary<string, string>>();
    }
    public static async Task Init(this WebApplication app)
    {
        // Console.WriteLine(_getBaseConfig());
        var store = new PostgresStore();
        var meow = await store.GetAsync<string>("test", "global");
        Console.WriteLine($"Retrieved from store: {meow}");
        
        const string prefix = "/api";
        const string targetUrl = "localhost:8000";
        var api = app.MapGroup(prefix)
            .WithOpenApi();

        var pluginManager = new PluginManager();
        await pluginManager.LoadPluginsAsync("services/plugins");

        var serviceConfigProvider = new TestConfigProvider();
        await serviceConfigProvider.InitialiseAsync(pluginManager, store);
        
        var requestPipeline = new RequestPipelineBuilder()
            .WithConfigProvider(serviceConfigProvider)
            .Build();
        
        
        api.Map("/{**path}", (HttpContext context, string path) =>
                {
                    var requestContext = new GatewayPluginContract.RequestContext
                    {
                        Request = context.Request,
                        Response = context.Response,
                        IsBlocked = false,
                        TargetPathBase = targetUrl,
                        PathPrefix = prefix
                    };
                    
                    return requestPipeline.ProcessAsync(requestContext);
                }
            ).WithName("ApiForwarder")
            .WithTags("Api")
            .Produces(StatusCodes.Status404NotFound);
            
    }
}

