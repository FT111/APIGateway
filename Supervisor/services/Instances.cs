using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;

namespace Supervisor.services;

public static class Instances
{
    public class ManagedInstance
    {
        public required Instance Instance { get; init;  }
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    }
    
    public class InstanceManager
    {
        private SupervisorAdapter Messages { get; }
        private InternalTypes.Repositories.Gateway GatewayData { get; }
        public Dictionary<Guid, ManagedInstance> Instances { get; } = new();

        public InstanceManager(SupervisorAdapter messages, InternalTypes.Repositories.Gateway gatewayData)
        {
            Messages = messages;
            GatewayData = gatewayData;
            
            // Load existing instances from the database
            var existingInstances = GatewayData.Context.Set<Instance>().ToList();
            foreach (var instance in existingInstances)
            {
                Instances[instance.Id] = new ManagedInstance { Instance = instance };
            }
            
        }
        
        public async Task StartAsync()
        {
            await Messages.SubscribeAsync(SupervisorEventType.Heartbeat, HandleHeartbeat);
            _ = HandleInstanceHealthCheckingAsync(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1));
        }
        
        private async Task HandleInstanceHealthCheckingAsync(TimeSpan checkInterval, TimeSpan timeout)
        {
            while (true)
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var offline = Instances.Where(kv => now - kv.Value.LastSeen > timeout).Select(kv => kv.Key).ToList();
                    foreach (var instanceId in offline)
                    {
                        Instances.Remove(instanceId);
                        var instance = await GatewayData.Context.Set<Instance>().FindAsync(instanceId);
                        if (instance != null)
                        {
                            GatewayData.Context.Set<Instance>().Entry(instance).Entity.Status = "offline";
                            await GatewayData.Context.SaveChangesAsync();
                        }
                    }
                    await Task.Delay(checkInterval);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during instance health check: {ex.Message}");
                }
            }
        }
        
        private Task HandleHeartbeat(SupervisorEvent e)
        {
            Console.WriteLine($"Received heartbeat event: {e.Value}");
            var instanceId = Guid.Parse(e.Value ?? throw new InvalidOperationException("Heartbeat event missing instance ID"));
            if (Instances.TryGetValue(instanceId, out var existingInstance))
            {
                existingInstance.LastSeen = DateTime.UtcNow;
            }
            else
            {
                if (GatewayData.Context.Set<Instance>().Any(i => i.Id == instanceId))
                {
                    var instance = GatewayData.Context.Set<Instance>().First(i => i.Id == instanceId);
                    Instances[instanceId] = new ManagedInstance { Instance = instance };
                }
                else {
                    throw new InvalidOperationException($"Received heartbeat from unknown instance ID {instanceId}");
                }
            }
            return Task.CompletedTask;
        }
    }
}