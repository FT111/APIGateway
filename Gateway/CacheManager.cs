using System.Collections.Concurrent;
using GatewayPluginContract;
using Microsoft.EntityFrameworkCore;

namespace Gateway;

public class CacheManager(StoreFactory stf) : PluginCacheManager(stf)
{
    private readonly ConcurrentDictionary<string, PluginCache> _caches = new ConcurrentDictionary<string, PluginCache>();
    private readonly DbContext _ctx = stf.CreateStore().Context;

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
        if (!_caches.TryGetValue(pluginIdentifier, out var cache))
        {
            cache = NewCache(pluginIdentifier);
        }
        
        return cache;
    }

    public override PluginCache NewCache(string pluginIdentifier)
    {
        Cache newCache = new Cache(_ctx);
        return !_caches.TryAdd(pluginIdentifier, newCache) ? throw new InvalidOperationException("Cache already exists") : newCache;
    }
}

public class Cache(DbContext ctx) : PluginCache(ctx)
{
    private ConcurrentDictionary<string, object> _data = new ConcurrentDictionary<string, object>();
    private ConcurrentDictionary<string, SemaphoreSlim> _locks = new ConcurrentDictionary<string, SemaphoreSlim>();
    private DbContext _ctx = ctx;


    
    public override T Get<T>(string key)
    {
        return (T)_data[key];
    }

    public override async Task Register<T>(string key, CachedData<T> data) where T : class
    {
        data.Data = await data.Fetch(_ctx);
        if (!_data.TryAdd(key, data) || !_locks.TryAdd(key, new SemaphoreSlim(1, 1)))
        {
            throw new InvalidOperationException("Item already exists");
        }
        _ = AddCacheDaemonAsync(data, key);
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
            
            data.Data = await data.Fetch(_ctx);
            _locks[key].Release();
        }
    }
}