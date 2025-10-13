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
            var target = context.Target;
            var existingRecord = stk.DataRepositories.GetRepo<PluginData>().QueryAsync(dt => (dt.Key == target.Id.ToString()) && (dt.Namespace==stk.Identity.OriginManifest.Name)).Result.FirstOrDefault() ?? throw new KeyNotFoundException("No existing record found for the IP address.");
            var assignedTarget = await stk.DataRepositories.Context.Set<Target>().FindAsync(existingRecord.Value);
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
            
            var assignedTarget = await stk.DataRepositories.Context.Set<Target>().FindAsync(availableVariations[variation]);
            context.Target = assignedTarget ?? throw new KeyNotFoundException("Assigned target not found in the database.");

            // Store the assigned variation in the scoped store
            async Task Task(CancellationToken cancellationToken, Repositories dataRepos)
            {
                var data = new PluginData
                {
                    Key = context.Request.HttpContext.Connection.RemoteIpAddress.MapToIPv4().ToString(),
                    Value = context.Target.Host,
                    Namespace = stk.Identity.OriginManifest.Name,
                };
                await dataRepos.GetRepo<PluginData>().UpdateAsync(data);
            }

            stk.DeferredTasks.QueueTask(Task);
        }
    }
}