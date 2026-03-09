using System.Diagnostics;
using GatewayPluginContract;
using GatewayPluginContract.Entities;
using GatewayPluginContract.MQ;
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
        // await HandlePluginDeliveryUrlUpdatesAsync();
    }

    private async Task SendHeartbeatAsync()
    {
        // Send a heartbeat to the Supervisor
        await _supervisor.SendEventAsync(new SupervisorEvent
        {
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
    
    // private async Task HandlePluginDeliveryUrlUpdatesAsync()
    // {
    //     await _supervisor.SubscribeAsync(DefaultMqCommands.UpdateDeliveryUrl, async (eventData) =>
    //     {
    //         if (eventData.Value != null && eventData.Value.StartsWith("http"))
    //         {
    //             _gateway.PluginManager.PluginDeliveryUrl = eventData.Value;
    //             
    //         }
    //     });
    // }
    //

    private async Task HandleSupervisorCommandsAsync()
    {
            await _supervisor.SubscribeAsync(SupervisorEventType.Command, async (SupervisorEvent eventData) =>
            {
                await ProcessCommandAsync(eventData);
            }, _gateway.Identity.Id);
    }
    
    private async Task ProcessCommandAsync(SupervisorEvent eventData)
    {
        // parse eventdata to a command key

        try
        {
            _gateway.Logger?.LogInformation($"Received supervisor command: {eventData.CommandKey} with value: {eventData.Value} (Corr. ID: {eventData.CorrelationId}");
            var cmd = _gateway.CommandManager.GetCommand(eventData.CommandKey);
            await cmd.Handler(_gateway, eventData.Value);
            _gateway.Logger?.LogInformation($"Handled supervisor command: {eventData.CommandKey} (Corr. ID {eventData.CorrelationId})");
        }
        catch (KeyNotFoundException ex)
        {
            _gateway.Logger?.LogError(ex, $"Received unknown supervisor command: {eventData.CommandKey} (Corr. ID {eventData.CorrelationId})");
            // command isn't registered
        }
        catch (Exception ex)
        {
            _gateway.Logger?.LogError(ex, $"Error processing supervisor command {eventData.CommandKey} (Corr. ID {eventData.CorrelationId})");
        }

        
        // switch (eventData.Type)
        // {
        //     case DefaultMqCommands.Restart:

        //     case DefaultMqCommands.UpdateRoutes:
        //     case DefaultMqCommands.PreloadRoutes:

        //         break;
        //     case DefaultMqCommands.ApplyBufferedRoutes:
        //        
        //     case DefaultMqCommands.Stop:
        //         var instance = await _context.Set<Instance>().Where(i => i.Id == _gateway.Identity.Id).FirstOrDefaultAsync();
        //         if (instance != null)
        //         {
        //             instance.Status = "offline";
        //             await _context.SaveChangesAsync();
        //         }
        //         await _context.DisposeAsync();
        //         Environment.Exit(0);
        //         break;
        // }
    }

}