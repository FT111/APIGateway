using GatewayPluginContract;
using GatewayPluginContract.Entities;

namespace Lecti;

public class Selector : IRequestProcessor
{
    public async Task ProcessAsync(RequestContext context, ServiceContext stk)
    {
        // Checks if the IP has already been given an A/B variation
        var clientIp = context.Request.HttpContext.Connection.RemoteIpAddress.MapToIPv4();
        try
        {
            // var existingRecord = stk.DataRepositories.GetRepo<PluginData>().QueryAsync(dt => (dt.Key == context.Request.HttpContext.Connection.RemoteIpAddress.MapToIPv4().ToString()) && (dt.Namespace==stk.Identity.OriginManifest.Name)).Result.FirstOrDefault() ?? throw new KeyNotFoundException("No existing record found for the IP address.");
            // Search the cache for a previously assigned target to this client
            var existingRecord = stk.Cache.Get<IQueryable<PluginData>>("assignedTargets")?.AsParallel().FirstOrDefault(dt =>
                dt.Key == clientIp.ToString());
            if (existingRecord == null) throw new KeyNotFoundException();

            var assignedTarget = stk.Cache.Get<IQueryable<Target>>("storedTargets")?.AsParallel().FirstOrDefault(t => t.Id == Guid.Parse(existingRecord.Value));
            context.Target = assignedTarget ??
                             throw new KeyNotFoundException("Assigned target not found in the database.");
        }
        catch (Exception)
        {
            // If not, randomly assign one of the variations
            var random = new Random();

            List<string> availableVariations =
                System.Text.Json.JsonSerializer.Deserialize<List<string>>(
                    context.PluginConfiguration[stk.Identity.OriginManifest.Name]["downstream_variants"]) ??
                throw new InvalidOperationException("No downstream variations configured.");
            var variation = random.Next(0, availableVariations.Count);

            var assignedTarget = await stk.DataRepositories.Context.Set<Target>()
                .FindAsync(Guid.Parse(availableVariations[variation]));
            context.Target = assignedTarget ??
                             throw new KeyNotFoundException("Assigned target not found in the database.");

            // Store the assigned variation in the scoped store
            async Task Task(CancellationToken cancellationToken, Repositories dataRepos)

            {
                var data = new PluginData
                {
                    Key = clientIp.ToString(),
                    Value = context.Target.Id.ToString(),
                    Namespace = stk.Identity.OriginManifest.Name,
                };
                await dataRepos.Context.Set<PluginData>().AddAsync(data, cancellationToken);
                await dataRepos.Context.SaveChangesAsync(cancellationToken);
            }

            stk.DeferredTasks.QueueTask(Task);
        }
    }
}