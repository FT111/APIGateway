using System.Diagnostics;
using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;

namespace SharedServices.Commands.Internal;

public class PreloadRoutes : InternalContracts.CommandDefinition
{
    public PreloadRoutes()
    {
        PluginIdentifier = "internal";
        Identifier = "routes.preload";
        Handler = async (gateway, param) => await Handle(gateway, param);
    }


    public async Task Handle(GatewayBase gateway, string? param)
    {
        gateway.Pipe.Router.BufferNewTrie(await gateway.RouterFactory.BuildRouteTrie());
        await gateway.SupervisorAdapter.SendEventAsync(new SupervisorEvent
        {   
            CommandKey = DefaultMqCommands.Response,
            Value = "preload_complete"
        });
    }
}