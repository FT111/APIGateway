using GatewayPluginContract;

namespace Lecti;

public class Selector : IRequestProcessor
{
    public async Task ProcessAsync(RequestContext context, IBackgroundQueue backgroundQueue, IScopedStore store)
    {
        // Checks if the IP has already been given an A/B variation
        try
        {
            var existingRecord =
                await store.GetAsync<string>(context.Request.HttpContext.Connection.RemoteIpAddress.MapToIPv4()
                    .ToString());
            context.TargetPathBase = existingRecord;
        }
        catch (KeyNotFoundException)
        {
            // If not, randomly assign one of the variations
            var random = new Random();
            Console.WriteLine(
                $"Assigning new Lecti variation for {context.Request.HttpContext.Connection.RemoteIpAddress}");


            List<string> availableVariations =
                System.Text.Json.JsonSerializer.Deserialize<List<string>>(
                    context.PluginConfiguration["Lecti0.1"]["downstream_variants"])
                ?? [context.TargetPathBase];
            var variation = random.Next(0, availableVariations.Count);
            context.TargetPathBase = availableVariations[variation];

            // Store the assigned variation in the scoped store
            async ValueTask Task(CancellationToken cancellationToken)
            {
                await store.SetAsync<string>(context.Request.HttpContext.Connection.RemoteIpAddress.MapToIPv4().ToString(), "text", context.TargetPathBase);
            }

            backgroundQueue.QueueTask(Task);
        }
    }
}