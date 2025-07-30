using System.Threading.Channels;
using Gateway.services;
using GatewayPluginContract;
namespace Gateway;
using DeferredFunc = Func<CancellationToken, IRepoFactory, Task>;

public class TaskQueue : IBackgroundQueue
{
    private readonly Channel<DeferredFunc> _queue;

    public TaskQueue(int capacity = 100)
    {
        _queue = Channel.CreateBounded<DeferredFunc>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });
    }

    public void QueueTask(DeferredFunc task)
    {
        ArgumentNullException.ThrowIfNull(task);

        var isEnqueued = _queue.Writer.TryWrite(task);
        if (!isEnqueued)
        {
            throw new InvalidOperationException("Failed to enqueue task. The queue is full.");
        }
       
    }
    
    public async Task<DeferredFunc> DequeueAsync(CancellationToken cancellationToken = default)
    {
        var result = await _queue.Reader.ReadAsync(cancellationToken);
        return result;
    }
}

public class TaskQueueHandler(StoreFactory storeProvider, TaskQueue taskQueue)
{

    internal async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var dataRepos = storeProvider.CreateStore().GetRepoFactory();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var task = await taskQueue.DequeueAsync(stoppingToken);
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