using System.Net;
using Gateway.services;
namespace Gateway.routes;


public static class ApiRoutes
{
    public static async Task Init(this WebApplication app)
    {
        const string prefix = "/api";
        const string targetUrl = "localhost:8000";
        var api = app.MapGroup(prefix)
            .WithOpenApi();

        var pluginManager = new PluginManager();
        await pluginManager.LoadPluginsAsync("services/plugins");
        
        var serviceConfigurations = new ServiceConfigurationManager
        {
            Provider = new TestConfigProvider()
        };
        await serviceConfigurations.LoadConfigurationsAsync(pluginManager.Registrar);
        Console.WriteLine(serviceConfigurations.GlobalConfiguration);
        
        var requestPipeline = new RequestPipelineBuilder()
            .WithConfigManager(serviceConfigurations)
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

