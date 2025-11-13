using System.Collections.Concurrent;
using GatewayPluginContract;
using Microsoft.EntityFrameworkCore;

namespace Gateway;

public class CacheManager: PluginCacheManager
{
    private readonly ConcurrentDictionary<string, PluginCache> _caches = new ConcurrentDictionary<string, PluginCache>();
    private readonly StoreFactory _ctx;
    private readonly CacheHandler _cacheHandler;
    private CancellationTokenSource _daemonCancellationTokenSource = new CancellationTokenSource();

    public CacheManager(StoreFactory stf) : base(stf)
    {
        _ctx = stf;
        _cacheHandler = new CacheHandler(stf);
        
        // Start the cache daemon
        Task.Run(() => _cacheHandler.RunAsync(_daemonCancellationTokenSource.Token));
    }
    
    public void ConfigurePluginManager(PluginManager pm)
    {
        pm.AddPluginLoadStep(plugin =>
            {
                plugin.ConfigureDataRegistries(GetCache(plugin.GetManifest().Name));
                return Task.CompletedTask;
            }
            );
    }
    
    public override PluginCache GetCache(string pluginIdentifier)
    {
        return _caches.GetOrAdd(pluginIdentifier, id => new Cache(_ctx, _cacheHandler));
    }

    internal void StopBackgroundRefresh()
    {
        _daemonCancellationTokenSource.Cancel();
    }
}

public class Cache(StoreFactory ctx, CacheHandler ch) : PluginCache(ctx, ch)
{
    private ConcurrentDictionary<string, object> _data = new ConcurrentDictionary<string, object>();
    private ConcurrentDictionary<string, SemaphoreSlim> _locks = new ConcurrentDictionary<string, SemaphoreSlim>();
    private StoreFactory _ctx = ctx;
    private CacheHandler _ch = ch;
    
    public override T Get<T>(string key)
    {
        var retrieved = (CachedData<T>)_data[key];
        return retrieved.Data;
    }

    public override async Task Register<T>(string key, CachedData<T> data) where T : class
    {
        data.Data = await data.Fetch(_ctx.CreateStore().Context);
        if (!_data.TryAdd(key, data) || !_locks.TryAdd(key, new SemaphoreSlim(1, 1)))
        {
            throw new InvalidOperationException("Item already exists");
        }
        _ch.Register(data, key, _locks[key]);
    }

    private async Task AddCacheDaemonAsync<T>(CachedData<T> data, string key) where T : class
    { 
        if (data.InvalidationFrequency == null) return;
        while (true)
        {
            await Task.Delay(data.InvalidationFrequency.Value);
            if (!_locks.ContainsKey(key))
            {
                _locks.TryAdd(key, new SemaphoreSlim(1, 1));
            }
            await _locks[key].WaitAsync();

            data.Data = await data.Fetch(_ctx.CreateStore().Context);
            _locks[key].Release();
        }
    }
}

// Replaces the many cache daemons
// One central handler that handles all cache invalidation and refetching
public class CacheHandler(StoreFactory ctx) : ICacheHandler
{
    private StoreFactory _ctx = ctx;
    private ConcurrentQueue<ICacheEntry> _newItems = new ConcurrentQueue<ICacheEntry>();
    
    public void Register<T>(CachedData<T> item, string pluginKey,SemaphoreSlim sem) where T : class
    {
        var newEntry = new CacheEntry<T>(item, sem, pluginKey);
        // TODO Fix this being thrown as an invalid cast
        _newItems.Enqueue(newEntry ?? throw new InvalidOperationException("Type mismatch in cache entry."));
    }
    
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            while (_newItems.TryDequeue(out var obj))
            {
                if (cancellationToken.IsCancellationRequested) break;
                
                
                var loop = HandleEntryAsync(obj, obj.CToken);
            }
            
            await Task.Delay(50, cancellationToken);
        }
    }
    
    private async Task HandleEntryAsync(ICacheEntry entry, CancellationToken cancellationToken)
    {
        await entry.RefetchAsync(_ctx, cancellationToken);
        if (entry.InvalidationFrequency == null) return;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(entry.InvalidationFrequency.Value, cancellationToken);
                await entry.RefetchAsync(_ctx, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                break;
            }
        }
    }
    
    // private async Task RefetchEntryAsync(ICacheEntry entry, CancellationToken tok)
    // {
    //     await entry.Semaphore.WaitAsync(tok);
    //     entry.Data = await entry.Data.(_ctx.CreateStore().Context);
    //     entry.Semaphore.Release();
    // }
}

internal interface ICacheEntry
{
    SemaphoreSlim Semaphore { get; }
    object Data { get; set; }
    string PluginKey { get; }
    CancellationToken CToken { get; }
    TimeSpan? InvalidationFrequency { get; }
    Task RefetchAsync(StoreFactory ctx, CancellationToken tok);

}

internal class CacheEntry<T>(CachedData<T> data, SemaphoreSlim semaphore, string pluginKey) : ICacheEntry  where T : class 
{
    CachedData<T> Data { get; } = data;
    public SemaphoreSlim Semaphore { get; } = semaphore;
    public string PluginKey { get; } = pluginKey;
    internal CancellationToken CToken = new CancellationTokenSource().Token;
    
    object ICacheEntry.Data
    {
        get => Data;
        set => Data.Data = (T)value;
    }
    
    CancellationToken ICacheEntry.CToken => CToken;
    TimeSpan? ICacheEntry.InvalidationFrequency => Data.InvalidationFrequency;
    async Task ICacheEntry.RefetchAsync(StoreFactory ctx, CancellationToken tok)
    {
        await Semaphore.WaitAsync(tok);
        try
        {
            Data.Data = await Data.Fetch(ctx.CreateStore().Context);
        }
        finally
        {
            Semaphore.Release();
        }
    }
}
