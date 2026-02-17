using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;

namespace SharedServices.Commands.Internal;

public class UpdateRoutes : InternalContracts.CommandDefinition
{
    public UpdateRoutes()
    {
        PluginIdentifier = "internal";
        Identifier = "routes.update";
        Handler = async (gateway, param) => await Handle(gateway, param);
    }

    
    public async Task Handle( GatewayBase gateway, string? param)
    {
        gateway.Pipe.Router.BufferNewTrie(await gateway.RouterFactory.BuildRouteTrie());
        gateway.Pipe.Router.SwapTries();
    }
}