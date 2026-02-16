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
        gateway.BufferedRouter = await gateway.CreateRouterAsync();
        await gateway.SendEventAsync(new SupervisorEvent
        {   
            Type = DefaultMqCommands.Response,
            Value = "preload_complete"
        });
        break;
    }
}