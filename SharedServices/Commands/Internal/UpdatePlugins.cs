using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;

namespace SharedServices.Commands.Internal;

public class UpdatePlugins : InternalContracts.CommandDefinition
{
    private async Task<bool> Handle(DbContext context, GatewayBase gateway)
    {
        // Reload plugins to ensure the latest state - Then verify 
        await gateway.PluginManager.LoadPluginsAsync("services/plugins");
        var pluginVerification =
            await gateway.PluginManager.VerifyInstalledPluginsAsync(context.Set<PipeService>().AsNoTracking()
                .AsQueryable());

        if (pluginVerification.IsValid)
        {
            return true;
        }
        
        try
        {
            foreach (var plugin in pluginVerification.Missing)
            {
                
                await gateway.PluginManager.DownloadAndInstallPluginAsync(plugin);
            }
                    
            // Clean up old plugins
            foreach (var plugin in pluginVerification.Removed)
            {
                await gateway.PluginManager.RemovePluginAsync(plugin);
            }
                    
            // Load new plugins
            await gateway.PluginManager.LoadPluginsAsync("services/plugins");
            // Initialise newly installed plugins
            gateway.PluginInitManager.InitialiseFromPluginManager(gateway.PluginManager);
                    
            var finalVerification =
                await gateway.PluginManager.VerifyInstalledPluginsAsync(context.Set<PipeService>()
                    .AsNoTracking().AsQueryable());

            if (!finalVerification.IsValid)
            {
                throw new InvalidOperationException("Plugin verification failed after attempting to download and install missing plugins.");
            }
        }
        catch (Exception ex)
        {
            
        }
    

        return false;
    }

}