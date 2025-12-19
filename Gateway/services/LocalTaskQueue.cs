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
            SingleReader = true
        });
    }

    public void QueueTask(Func<CancellationToken, Repositories, Activity, ILogger, Task> task)
    {
        ArgumentNullException.ThrowIfNull(task);

        var isEnqueued = _queue.Writer.TryWrite(task);
        if (!isEnqueued)
        {
            throw new InvalidOperationException("Failed to enqueue task. The queue is full.");
        }
       
    }
    
    public async Task<Func<CancellationToken, Repositories, Activity, ILogger, Task>> DequeueAsync(CancellationToken cancellationToken = default)
    {
        var result = await _queue.Reader.ReadAsync(cancellationToken);
        return result;
    }
}