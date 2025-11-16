using System.Net;
using Gateway.services;
using GatewayPluginContract;
using GatewayPluginContract.Entities;

namespace Gateway.routes;


public class Proxy
{
    public async Task Init(WebApplication app, Gateway gateway)
    {
        
        const string prefix = "/";
        var api = app.MapGroup(prefix)
            .WithOpenApi();
        
        
        api.Map("/{**path}", (HttpContext context, string path, ILogger<Proxy> logger) =>
                {
                    var requestContext = new GatewayPluginContract.RequestContext
                    {
                        Request = context.Request,
                        Response = context.Response,
                        Logger = logger,
                        LogRequest = new Request()
                        {
                            Id = Guid.NewGuid()
                        },
                        GatewayPathPrefix = prefix
                    };
                    
                    return gateway.Pipe.ProcessAsync(requestContext, context);
                }
            ).WithName("ApiForwarder")
            .WithTags("Api")
            .Produces(StatusCodes.Status404NotFound);
            
    }
}

