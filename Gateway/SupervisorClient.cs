using System.Diagnostics;
using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gateway;

/// <summary>
/// A client for the Gateway Supervisor.
/// Communicates with the Supervisor, handling heartbeats, plugin updates, and manager commands.
/// </summary>
public class SupervisorClient(
    SupervisorAdapter supervisor,
    Gateway gateway
    )
{
    private readonly DbContext _context = gateway.Store.CreateStore().Context;
    private readonly SupervisorAdapter _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));

    public async Task StartAsync()
    {
        // Start the heartbeat loop
        var heartbeatInterval = TimeSpan.FromSeconds(30);
        _ = StartHeartbeatLoopAsync(heartbeatInterval);
        // Handle Supervisor commands
        await HandleSupervisorCommandsAsync();
        // Handle plugin delivery URL updates
        await HandlePluginDeliveryUrlUpdatesAsync();
    }

    private async Task SendHeartbeatAsync()
    {
        // Send a heartbeat to the Supervisor
        await _supervisor.SendEventAsync(new SupervisorEvent
        {
            Type = SupervisorEventType.Heartbeat,
            Value = gateway.Identity.Id.ToString()
        });
    }
    private async Task StartHeartbeatLoopAsync(TimeSpan interval)
    {
        while (true)
        {
            try
            {
                await SendHeartbeatAsync();
                await Task.Delay(interval);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending initial heartbeat: {ex.Message}");
            }
        }
    }
    
    private async Task HandlePluginDeliveryUrlUpdatesAsync()
    {
        await _supervisor.SubscribeAsync(SupervisorEventType.DeliveryUrl, async (eventData) =>
        {
            if (eventData.Value != null && eventData.Value.StartsWith("http"))
            {
                gateway.PluginManager.PluginDeliveryUrl = eventData.Value;
                Console.WriteLine($"Updated plugin delivery URL to: {eventData.Value}");
            }
        });
    }
    
    private async Task HandleSupervisorCommandsAsync()
    {
        await _supervisor.SubscribeAsync(SupervisorEventType.Command, async (SupervisorEvent eventData) =>
        {
            await ProcessCommandAsync(eventData);
        }, gateway.Identity.Id);
        // // Subscribe to Supervisor commands
        // await _supervisor.SubscribeAsync(SupervisorEventType.Command, async (eventData) =>
        // {
        //     await ProcessCommandAsync(eventData);
        // });
    }
    
    private async Task ProcessCommandAsync(SupervisorEvent eventData)
    {
        switch (eventData.Value)
        {
            case "update_plugins":
                Console.WriteLine("Received plugin update command. Updating plugins...");

                if (await HandlePluginDiscrepancies()) return;

                break;
            case "restart":
                Console.WriteLine("Received gateway restart command. Restarting gateway...");
                var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                var newProcess = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = currentProcess.MainModule?.FileName ?? throw new InvalidOperationException("Cannot determine current process file name"),
                    Arguments = string.Join(' ', Environment.GetCommandLineArgs().Skip(1)),
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                };
                System.Diagnostics.Process.Start(newProcess);
                await Task.Delay(1000); // Give the new process a moment to start
                Environment.Exit(0);
                break;
            case "update_routes":
                await gateway.RebuildRouterAsync();
                break;
            case "stop":
                Console.WriteLine("Received gateway stop command. Stopping gateway...");
                var instance = await _context.Set<Instance>().Where(i => i.Id == gateway.Identity.Id).FirstOrDefaultAsync();
                if (instance != null)
                {
                    instance.Status = "offline";
                    await _context.SaveChangesAsync();
                }
                await _context.DisposeAsync();
                
                Environment.Exit(0);
                break;
            default:
                Console.WriteLine($"Unknown command received: {eventData.Value}");
                break;
        }
        await Task.CompletedTask;
    }

    private async Task<bool> HandlePluginDiscrepancies()
    {
        // Reload plugins to ensure the latest state - Then verify 
        await gateway.PluginManager.LoadPluginsAsync("services/plugins");
        var pluginVerification =
            await gateway.PluginManager.VerifyInstalledPluginsAsync(_context.Set<PipeService>().AsNoTracking()
                .AsQueryable());

        if (pluginVerification.IsValid)
        {
            return true;
        }
        
        try
        {
            foreach (var plugin in pluginVerification.Missing)
            {
                Console.WriteLine($"Downloading and installing plugin: {plugin}");
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
                await gateway.PluginManager.VerifyInstalledPluginsAsync(_context.Set<PipeService>()
                    .AsNoTracking().AsQueryable());

            if (!finalVerification.IsValid)
            {
                throw new InvalidOperationException("Plugin verification failed after attempting to download and install missing plugins.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error downloading or installing plugins: {ex.Message}");
        }
    

        return false;
    }
}