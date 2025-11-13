using GatewayPluginContract;
using GatewayPluginContract.Entities;

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
        
        if (context.Response.StatusCode.ToString()[0] != '5' && !context.IsForwardingFailed) return Task.CompletedTask;
        
        

        List<string> availableVariations =
            System.Text.Json.JsonSerializer.Deserialize<List<string>>(
                context.PluginConfiguration[stk.Identity.OriginManifest.Name]["downstream_variants"]) 
            ?? [context.Target.Host];

        availableVariations.Remove(context.Target.Host);

        if (availableVariations.Count > 0)
        {
            // Randomly select a new variation
            var random = new Random();
            var newVariation = availableVariations[random.Next(0, availableVariations.Count)];

            async Task Task(CancellationToken cancellationToken, Repositories dataRepos)
            {
                var data = new PluginData
                {
                    Key = context.Request.HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? throw new ArgumentNullException(),
                    Value = newVariation,
                    Namespace = stk.Identity.OriginManifest.Name,
                };
                await dataRepos.Context.Set<PluginData>().AddAsync(data, cancellationToken);
            }

            stk.DeferredTasks.QueueTask(Task);
            context.Target.Host = newVariation;
            context.IsRestartRequested = true;

            
        }
        else
        {
            
        }
        return Task.CompletedTask;

    }
}