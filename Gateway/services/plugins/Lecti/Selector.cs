using GatewayPluginContract;

namespace Lecti;

public class Selector : IRequestProcessor
{
    public async Task ProcessAsync(RequestContext context, ServiceContext stk)
    {
        // Checks if the IP has already been given an A/B variation
        try
        {
            var existingRecord =
                await stk.RepoFactory.GetRepo<PluginData>().GetAsync(context.Request.HttpContext.Connection.RemoteIpAddress.MapToIPv4()
                    .ToString(), stk.Identity.OriginManifest.Name) ?? throw new KeyNotFoundException("No existing record found for the IP address.");
            context.TargetPathBase = existingRecord.Value;
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
                var data = new PluginData
                {
                    Key = context.Request.HttpContext.Connection.RemoteIpAddress.MapToIPv4().ToString(),
                    Value = context.TargetPathBase,
                    Namespace = stk.Identity.OriginManifest.Name,
                };
                // await store.SetAsync<string>(context.Request.HttpContext.Connection.RemoteIpAddress.MapToIPv4().ToString(), "text", context.TargetPathBase);
                await stk.RepoFactory.GetRepo<PluginData>().UpdateAsync(data);
            }

            stk.DeferredTasks.QueueTask(Task);
        }
    }
}