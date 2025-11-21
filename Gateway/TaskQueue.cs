using System.Diagnostics;
using Gateway.services;
using GatewayPluginContract;
namespace Gateway;

public class TaskQueueHandler(StoreFactory storeProvider, LocalTaskQueue localTaskQueue)
{
    private ILogger? _logger;
    internal async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var task = await localTaskQueue.DequeueAsync(stoppingToken);
                var dataRepos = storeProvider.CreateStore().GetRepoFactory();
                using (Activity.Current = new Activity("BackgroundTaskExecution").Start())
                {
                    await task(stoppingToken, dataRepos, Activity.Current, _logger);
                }
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

    public void AddLogger(ILogger logger)
    {
        _logger = logger;
    }
}