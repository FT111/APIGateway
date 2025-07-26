using GatewayPluginContract;

namespace Lecti;

/// <summary>
/// Handles error fallbacks.
/// If the chosen variation fails, it will pick another one.
/// </summary>
public class Checker : IRequestProcessor
{
    public Task ProcessAsync(RequestContext context, ServiceContext stk)
    {
        // Reroute the request if the response is not successful
        Console.WriteLine($"Checking response status code: {context.Response.StatusCode} for request to {context.Request.Path}");
        if (context.Response.StatusCode.ToString()[0] != '5' && !context.IsForwardingFailed) return Task.CompletedTask;
        
        Console.WriteLine($"Response from {context.TargetPathBase} failed with status code {context.Response.StatusCode} and {context.IsForwardingFailed} fault. Checking for fallbacks...");

        List<string> availableVariations =
            System.Text.Json.JsonSerializer.Deserialize<List<string>>(
                context.PluginConfiguration["Lecti0.1"]["downstream_variants"]) 
            ?? [context.TargetPathBase];

        availableVariations.Remove(context.TargetPathBase);

        if (availableVariations.Count > 0)
        {
            // Randomly select a new variation
            var random = new Random();
            var newVariation = availableVariations[random.Next(0, availableVariations.Count)];

            async ValueTask Task(CancellationToken cancellationToken)
            {
                var data = new PluginData
                {
                    Key = context.Request.HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? throw new ArgumentNullException(),
                    Value = newVariation,
                    Namespace = stk.Identity.OriginManifest.Name,
                };
                await stk.RepoFactory.GetRepo<PluginData>().UpdateAsync(data);
            }

            stk.DeferredTasks.QueueTask(Task);
            context.TargetPathBase = newVariation;
            context.IsRestartRequested = true;

            Console.WriteLine($"Falling back to {newVariation} for request to {context.Request.Path}");
        }
        else
        {
            Console.WriteLine("No fallback variations available.");
        }
        return Task.CompletedTask;

    }
}