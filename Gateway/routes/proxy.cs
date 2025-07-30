using System.Net;
using Gateway.services;
using GatewayPluginContract;

namespace Gateway.routes;


public static class Proxy
{
    public static async Task Init(this WebApplication app)
    {
        var storeProvider = new EfStoreFactory(app.Configuration);
        var store = storeProvider.CreateStore();
        var repoFactory = store.GetRepoFactory();
        
        const string prefix = "/";
        var api = app.MapGroup(prefix)
            .WithOpenApi();

        var backgroundQueue = new TaskQueue();
        var queueHandler = new TaskQueueHandler(storeProvider, backgroundQueue);
        queueHandler.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
        
        var pluginManager = new PluginManager();
        await pluginManager.LoadPluginsAsync("services/plugins");

        var serviceConfigProvider = new ConfigProvider();
        await serviceConfigProvider.InitialiseAsync(pluginManager, repoFactory);
        
        var requestPipeline = new RequestPipelineBuilder()
            .WithConfigProvider(serviceConfigProvider)
            .WithRepoProvider(repoFactory)
            .WithBackgroundQueue(backgroundQueue)
            .Build();
        
        api.MapGet("/", () => Results.Ok("API Gateway is running!"))
            .WithName("ApiRoot")
            .WithTags("Api")
            .Produces(StatusCodes.Status200OK);
        api.Map("/{**path}", (HttpContext context, string path) =>
                {
                    var requestContext = new GatewayPluginContract.RequestContext
                    {
                        Request = context.Request,
                        Response = context.Response,
                        GatewayPathPrefix = prefix
                    };
                    
                    return requestPipeline.ProcessAsync(requestContext, context);
                }
            ).WithName("ApiForwarder")
            .WithTags("Api")
            .Produces(StatusCodes.Status404NotFound);
            
    }
}

