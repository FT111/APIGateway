using System.Diagnostics;
using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;

namespace SharedServices.Commands.Internal;

public class Stop : InternalContracts.CommandDefinition
{
    public Stop()
    {
        PluginIdentifier = "internal";
        Identifier = "instance.stop";
        Handler = async (gateway, param) => await Handle(gateway, param);
    }


    public async Task Handle(GatewayBase gateway, string? param)
    {
        var context = gateway.Store.CreateStore().Context;
        var instance = await context.Set<Instance>().Where(i => i.Id == gateway.Identity.Id).FirstOrDefaultAsync();
        if (instance != null)
        {
            instance.Status = "offline";
            await context.SaveChangesAsync();
        }
        await context.DisposeAsync();
        Environment.Exit(0);
    }
}