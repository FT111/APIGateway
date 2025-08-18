using Gateway.services;
using GatewayPluginContract;
namespace Gateway;

public class TaskQueueHandler(StoreFactory storeProvider, LocalTaskQueue localTaskQueue)
{
    internal async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var dataRepos = storeProvider.CreateStore().GetRepoFactory();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var task = await localTaskQueue.DequeueAsync(stoppingToken);
                await task(stoppingToken, dataRepos);
            }
            catch (OperationCanceledException)
            {
                // Task was cancelled, continue to next iteration
                continue;
            }
            catch (Exception ex)
            {
                // Log the exception or handle it as needed
                Console.WriteLine($"Error processing deferred task: {ex.StackTrace}");
            }
        }
    }
}