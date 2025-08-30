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
    private readonly SupervisorAdapter _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));

    public async Task StartAsync()
    {
        // Start the heartbeat loop
        var heartbeatInterval = TimeSpan.FromSeconds(30);
        _ = StartHeartbeatLoopAsync(heartbeatInterval);
        // Handle Supervisor commands
        await HandleSupervisorCommandsAsync();
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
    
    private async Task HandleSupervisorCommandsAsync()
    {
        await _supervisor.SubscribeAsync(SupervisorEventType.Command, async (SupervisorEvent eventData) =>
        {
            await ProcessCommandAsync(eventData);
        }, gateway.Identity.Id);
        // Subscribe to Supervisor commands
        await _supervisor.SubscribeAsync(SupervisorEventType.Command, async (eventData) =>
        {
            await ProcessCommandAsync(eventData);
        });
    }
    
    private async Task ProcessCommandAsync(SupervisorEvent eventData)
    {
        switch (eventData.Value)
        {
            case "update_plugins":
                Console.WriteLine("Received plugin update command. Updating plugins...");
                // This will fetch plugins from the DB in the future
                await gateway.PluginManager.LoadPluginsAsync(gateway.BaseConfiguration["PluginDirectory"] ?? "services/plugins");
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
            case "update_configurations":
                gateway.ConfigurationsProvider.LoadPipeConfigs();
                gateway.ConfigurationsProvider.LoadServiceConfigs();
                break;
            case "update_routes":
                await gateway.RebuildRouterAsync();
                break;
            case "stop":
                Console.WriteLine("Received gateway stop command. Stopping gateway...");
                var context = gateway.Store.CreateStore().Context;
                var instance = await context.Set<Instance>().Where(i => i.Id == gateway.Identity.Id).FirstOrDefaultAsync();
                if (instance != null)
                {
                    instance.Status = "offline";
                    await context.SaveChangesAsync();
                }
                await context.DisposeAsync();
                
                Environment.Exit(0);
                break;
            default:
                Console.WriteLine($"Unknown command received: {eventData.Value}");
                break;
        }
        await Task.CompletedTask;
    }
}