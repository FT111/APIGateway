using GatewayPluginContract;

namespace Lecti;

public class LectiSelector : IRequestProcessor
{
    public async Task ProcessAsync(IRequestContext context, List<Func<Task>> deferredTasks, IScopedStore store)
    {
        // Checks if the IP has already been given an A/B variation
        try
        {
            var existingRecord = await store.GetAsync<string>(context.Request.HttpContext.Connection.RemoteIpAddress.MapToIPv4().ToString());
            context.TargetPathBase = existingRecord;
        }
        catch (KeyNotFoundException)
        {
            // If not, randomly assign one of the variations
            var random = new Random();
            Console.WriteLine($"Assigning new variation for {context.Request.HttpContext.Connection.RemoteIpAddress}");
            Console.WriteLine($"Available variations: {context.PluginConfiguration["Lecti0.1"]["downstream_variants"]}");
            foreach (var ip in System.Text.Json.JsonSerializer.Deserialize<List<string>>(context.PluginConfiguration["Lecti0.1"]["downstream_variants"])!)
            {
                Console.WriteLine($"Available variation: {ip}");
            }

            List<string>? availableVariations = System.Text.Json.JsonSerializer.Deserialize<List<string>>(context.PluginConfiguration["Lecti0.1"]["downstream_variants"])
                ?? throw new InvalidOperationException("No downstream variants configured in plugin settings.");
            var variation = random.Next(0, availableVariations.Count);
            Console.WriteLine($"Assigned variation: {variation}");
            context.TargetPathBase = availableVariations[variation];

            // Store the assigned variation in the scoped store
            await store.SetAsync<string>(context.Request.HttpContext.Connection.RemoteIpAddress.MapToIPv4().ToString(), "text", context.TargetPathBase);
        }
    }
}