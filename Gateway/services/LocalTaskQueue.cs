using System.Threading.Channels;
using GatewayPluginContract;

namespace Gateway.services;

public class LocalTaskQueue : IBackgroundQueue
{
    private readonly Channel<Func<CancellationToken, IGatewayRepositories, Task>> _queue;

    public LocalTaskQueue(IConfiguration configuration)
    {
        _queue = Channel.CreateBounded<Func<CancellationToken, IGatewayRepositories, Task>>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });
    }

    public void QueueTask(Func<CancellationToken, IGatewayRepositories, Task> task)
    {
        ArgumentNullException.ThrowIfNull(task);

        var isEnqueued = _queue.Writer.TryWrite(task);
        if (!isEnqueued)
        {
            throw new InvalidOperationException("Failed to enqueue task. The queue is full.");
        }
       
    }
    
    public async Task<Func<CancellationToken, IGatewayRepositories, Task>> DequeueAsync(CancellationToken cancellationToken = default)
    {
        var result = await _queue.Reader.ReadAsync(cancellationToken);
        return result;
    }
}