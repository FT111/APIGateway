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
        
        public async Task DeleteInstanceAsync(Guid instanceId)
        {
            if (Instances.ContainsKey(instanceId))
            {
                var instance = await GatewayData.Context.Set<Instance>().FindAsync(instanceId);
                if (instance != null)
                {
                    GatewayData.Context.Set<Instance>().Remove(instance);
                    await GatewayData.Context.SaveChangesAsync();
                }
                Instances.Remove(instanceId);
                await Messages.SendEventAsync(new SupervisorEvent
                {
                    Type = SupervisorEventType.Stop,
                    Value = instanceId.ToString()
                }, instanceId);
            }
            else
            {
                throw new InvalidOperationException($"Instance with ID {instanceId} not found");
            }
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
                        var instance = await GatewayData.Context.Set<Instance>().FindAsync(instanceId);
                        Instances.Where(kv => kv.Key == instanceId).ToList().ForEach(kv => kv.Value.Instance.Status = "offline");
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
                    
                }
            }
        }
        
        private Task HandleHeartbeat(SupervisorEvent e)
        {
            
            var instanceId = Guid.Parse(e.Value ?? throw new InvalidOperationException("Heartbeat event missing instance ID"));
            if (Instances.TryGetValue(instanceId, out var existingInstance))
            {
                existingInstance.Instance.Status = "online";
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