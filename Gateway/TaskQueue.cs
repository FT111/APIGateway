using System.Threading.Channels;
using GatewayPluginContract;

namespace Gateway;

public class TaskQueue : IBackgroundQueue
{
    private readonly Channel<Func<CancellationToken, ValueTask>> _queue;

    public TaskQueue(int capacity = 100)
    {
        _queue = Channel.CreateBounded<Func<CancellationToken, ValueTask>>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });
    }

    public void QueueTask(Func<CancellationToken, ValueTask> task)
    {
        ArgumentNullException.ThrowIfNull(task);

        var isEnqueued = _queue.Writer.TryWrite(task);
        if (!isEnqueued)
        {
            throw new InvalidOperationException("Failed to enqueue task. The queue is full.");
        }
       
    }
    
    public async Task<Func<CancellationToken, ValueTask>> DequeueAsync(CancellationToken cancellationToken = default)
    {
        var result = await _queue.Reader.ReadAsync(cancellationToken);
        return result;
    }
}

public class TaskQueueHandler
{
    private readonly TaskQueue _taskQueue;

    public TaskQueueHandler(TaskQueue taskQueue)
    {
        _taskQueue = taskQueue;
    } 
    internal async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var task = await _taskQueue.DequeueAsync(stoppingToken);
                await task(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Task was cancelled, continue to next iteration
                continue;
            }
            catch (Exception ex)
            {
                // Log the exception or handle it as needed
                Console.WriteLine($"Error processing task: {ex.Message}");
            }
        }
    }
}