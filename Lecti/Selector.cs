using GatewayPluginContract;
using GatewayPluginContract.Entities;

namespace Lecti;

public class Selector : IRequestProcessor
{
    public async Task ProcessAsync(RequestContext context, ServiceContext stk)
    {
        // Checks if the IP has already been given an A/B variation
        try
        {
            Console.WriteLine(
                $"Checking existing Lecti variation for {context.Request.HttpContext.Connection.RemoteIpAddress.MapToIPv4()}, {stk.Identity.OriginManifest.Name}");
            
            // var existingRecord = stk.DataRepositories.GetRepo<PluginData>().QueryAsync(dt => (dt.Key == context.Request.HttpContext.Connection.RemoteIpAddress.MapToIPv4().ToString()) && (dt.Namespace==stk.Identity.OriginManifest.Name)).Result.FirstOrDefault() ?? throw new KeyNotFoundException("No existing record found for the IP address.");
            // Search the cache for a previously assigned target to this client
            var existingRecord = stk.Cache.Get<IQueryable<PluginData>>("assignedTargets")?.FirstOrDefault(dt =>
                dt.Key == context.Request.HttpContext.Connection.RemoteIpAddress.MapToIPv4().ToString());
            if (existingRecord == null) throw new KeyNotFoundException();
            
            var assignedTarget = await stk.DataRepositories.Context.Set<Target>().FindAsync(Guid.Parse(existingRecord.Value));
            context.Target = assignedTarget ?? throw new KeyNotFoundException("Assigned target not found in the database.");
        }
        catch (Exception)
        {
            // If not, randomly assign one of the variations
            var random = new Random();
            Console.WriteLine(
                $"Assigning new Lecti variation for {context.Request.HttpContext.Connection.RemoteIpAddress.MapToIPv4()}");


            List<string> availableVariations =
                System.Text.Json.JsonSerializer.Deserialize<List<string>>(
                    context.PluginConfiguration[stk.Identity.OriginManifest.Name]["downstream_variants"]) ?? throw new InvalidOperationException("No downstream variations configured.");
            var variation = random.Next(0, availableVariations.Count);
            
            var assignedTarget = await stk.DataRepositories.Context.Set<Target>().FindAsync(Guid.Parse(availableVariations[variation]));
            context.Target = assignedTarget ?? throw new KeyNotFoundException("Assigned target not found in the database.");

            // Capture values before queuing deferred task to avoid accessing disposed HTTP context
            var ipAddress = context.Request.HttpContext.Connection.RemoteIpAddress.MapToIPv4().ToString();
            var targetId = context.Target.Id.ToString();
            var pluginNamespace = stk.Identity.OriginManifest.Name;

            // Store the assigned variation in the scoped store
            async Task Task(CancellationToken cancellationToken, Repositories dataRepos)
            {
                var data = new PluginData
                {
                    Key = ipAddress,
                    Value = targetId,
                    Namespace = pluginNamespace,
                };
                await dataRepos.Context.Set<PluginData>().AddAsync(data, cancellationToken);
                await dataRepos.Context.SaveChangesAsync(cancellationToken);
            }

            stk.DeferredTasks.QueueTask(Task);
        }
    }
}