using System.Diagnostics;
using System.Threading.Channels;
using GatewayPluginContract;

namespace Gateway.services;

public class LocalTaskQueue : IBackgroundQueue
{
    private readonly Channel<Func<CancellationToken, Repositories, Activity, ILogger, Task>> _queue;

    public LocalTaskQueue(IConfiguration configuration)
    {
        _queue = Channel.CreateBounded<Func<CancellationToken, Repositories, Activity, ILogger, Task>>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            Capacity = 1000000000
        });
    }

    public void QueueTask(Func<CancellationToken, Repositories, Activity, ILogger, Task> task)
    {
        ArgumentNullException.ThrowIfNull(task);

        var isEnqueued = _queue.Writer.TryWrite(task);
        if (!isEnqueued)
        {
            Activity.Current?.AddTag("TaskQueue", "EnqueueFailed");
        }
       
    }
    
    public async Task<Func<CancellationToken, Repositories, Activity, ILogger, Task>> DequeueAsync(CancellationToken cancellationToken = default)
    {
        var result = await _queue.Reader.ReadAsync(cancellationToken);
        return result;
    }
}