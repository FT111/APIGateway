using Gateway.services;
using GatewayPluginContract;
namespace Gateway;

public class TaskQueueHandler(StoreFactory storeProvider, LocalTaskQueue localTaskQueue)
{
    internal async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var task = await localTaskQueue.DequeueAsync(stoppingToken);
                var dataRepos = storeProvider.CreateStore().GetRepoFactory();
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
                
            }
        }
    }
}