using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;

namespace SharedServices.Commands.Internal;

public class UpdateRoutes : InternalContracts.CommandDefinition
{
    public UpdateRoutes()
    {
        Identifier = "routes.update";
        Handler = async (gateway, param) => await Handle(gateway, param);
    }

    
    public static async Task Handle( GatewayBase gateway, string? param)
    {
        gateway.Pipe.Router = await gateway.CreateRouterAsync();
    }

}