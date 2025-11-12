using System.Collections.Concurrent;
using GatewayPluginContract;
using Microsoft.EntityFrameworkCore;

namespace Gateway;

public class CacheManager(StoreFactory stf) : PluginCacheManager(stf)
{
    private readonly ConcurrentDictionary<string, PluginCache> _caches = new ConcurrentDictionary<string, PluginCache>();
    private readonly StoreFactory _ctx = stf;

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
        return _caches.GetOrAdd(pluginIdentifier, id => new Cache(_ctx));
    }
}

public class Cache(StoreFactory ctx) : PluginCache(ctx)
{
    private ConcurrentDictionary<string, object> _data = new ConcurrentDictionary<string, object>();
    private ConcurrentDictionary<string, SemaphoreSlim> _locks = new ConcurrentDictionary<string, SemaphoreSlim>();
    private StoreFactory _ctx = ctx;


    
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
        _ = AddCacheDaemonAsync(data, key).ConfigureAwait(false);
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

public class CacheHandler(StoreFactory ctx)
{
    private StoreFactory _ctx = ctx;
    private ConcurrentQueue
}