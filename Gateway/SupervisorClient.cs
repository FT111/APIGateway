using System.Diagnostics;
using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gateway;

/// <summary>
/// A client for the Gateway Supervisor.
/// Communicates with the Supervisor, handling heartbeats, plugin updates, and manager commands.
/// </summary>
public class SupervisorClient
{
    private readonly DbContext _context;
    private readonly SupervisorAdapter _supervisor;
    private readonly Dictionary<string, Func<SupervisorEvent, Task>> _customEventHandlers = new();
    private readonly Gateway _gateway;

    /// <summary>
    /// A client for the Gateway Supervisor.
    /// Communicates with the Supervisor, handling heartbeats, plugin updates, and manager commands.
    /// </summary>
    public SupervisorClient(SupervisorAdapter supervisor,
        Gateway gateway)
    {
        _gateway = gateway;
        _context = gateway.Store.CreateStore().Context;
        _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
        _gateway.AddCustomSupervisorHandler = AddSupervisorEventHandler;
        _gateway.SendSupervisorEvent = supervisor.SendEventAsync;
    }


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
            Value = _gateway.Identity.Id.ToString()
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
                
            }
        }
    }

    internal Task AddSupervisorEventHandler(string key, Func<SupervisorEvent, Task> handler)
    {
        _customEventHandlers.Add(key, handler);
        return Task.CompletedTask;
    }
    
    private async Task HandlePluginDeliveryUrlUpdatesAsync()
    {
        await _supervisor.SubscribeAsync(SupervisorEventType.DeliveryUrl, async (eventData) =>
        {
            if (eventData.Value != null && eventData.Value.StartsWith("http"))
            {
                _gateway.PluginManager.PluginDeliveryUrl = eventData.Value;
                
            }
        });
    }

    private static readonly HashSet<SupervisorEventType> CommandTypes = new()
    {
        SupervisorEventType.UpdatePlugins,
        SupervisorEventType.Restart,
        SupervisorEventType.UpdateRoutes,
        SupervisorEventType.PreloadRoutes,
        SupervisorEventType.ApplyBufferedRoutes,
        SupervisorEventType.Stop
    };

    private async Task HandleSupervisorCommandsAsync()
    {
        // Subscribe to all specific command types
        foreach (var commandType in CommandTypes)
        {
            await _supervisor.SubscribeAsync(commandType, async (SupervisorEvent eventData) =>
            {
                await ProcessCommandAsync(eventData);
            }, _gateway.Identity.Id);
        }
    }
    
    private async Task ProcessCommandAsync(SupervisorEvent eventData)
    {
        if (_customEventHandlers.TryGetValue(eventData.Type.ToString(), out var func))
        {
            await func(eventData);
            return;
        }
        
        switch (eventData.Type)
        {
            case SupervisorEventType.UpdatePlugins:
                if (await HandlePluginDiscrepancies()) return;
                break;
            case SupervisorEventType.Restart:
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
            case SupervisorEventType.UpdateRoutes:
                _gateway.Pipe.Router = await _gateway.CreateRouterAsync();
                break;
            case SupervisorEventType.PreloadRoutes:
                _gateway.BufferedRouter = await _gateway.CreateRouterAsync();
                await _supervisor.SendEventAsync(new SupervisorEvent
                {   
                    Type = SupervisorEventType.Response,
                    Value = "preload_complete"
                });
                break;
            case SupervisorEventType.ApplyBufferedRoutes:
                if (_gateway.BufferedRouter != null)
                {
                    _gateway.Pipe.Router = _gateway.BufferedRouter;
                    _gateway.BufferedRouter = null;
                }
                break;
            case SupervisorEventType.Stop:
                var instance = await _context.Set<Instance>().Where(i => i.Id == _gateway.Identity.Id).FirstOrDefaultAsync();
                if (instance != null)
                {
                    instance.Status = "offline";
                    await _context.SaveChangesAsync();
                }
                await _context.DisposeAsync();
                Environment.Exit(0);
                break;
        }
    }

    private async Task<bool> HandlePluginDiscrepancies()
    {
        // Reload plugins to ensure the latest state - Then verify 
        await _gateway.PluginManager.LoadPluginsAsync("services/plugins");
        var pluginVerification =
            await _gateway.PluginManager.VerifyInstalledPluginsAsync(_context.Set<PipeService>().AsNoTracking()
                .AsQueryable());

        if (pluginVerification.IsValid)
        {
            return true;
        }
        
        try
        {
            foreach (var plugin in pluginVerification.Missing)
            {
                
                await _gateway.PluginManager.DownloadAndInstallPluginAsync(plugin);
            }
                    
            // Clean up old plugins
            foreach (var plugin in pluginVerification.Removed)
            {
                await _gateway.PluginManager.RemovePluginAsync(plugin);
            }
                    
            // Load new plugins
            await _gateway.PluginManager.LoadPluginsAsync("services/plugins");
            // Initialise newly installed plugins
            _gateway.PluginInitManager.InitialiseFromPluginManager(_gateway.PluginManager);
                    
            var finalVerification =
                await _gateway.PluginManager.VerifyInstalledPluginsAsync(_context.Set<PipeService>()
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