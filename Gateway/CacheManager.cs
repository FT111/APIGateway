using System.Collections.Concurrent;
using GatewayPluginContract;

namespace Gateway;

public class CacheManager : IPluginCacheManager
{
    private ConcurrentDictionary<string, IPluginCache> _caches = new ConcurrentDictionary<string, IPluginCache>();
    
    public IPluginCache GetCache(string pluginIdentifier)
    {
        return _caches[pluginIdentifier];
    }

    public IPluginCache NewCache(string pluginIdentifier)
    {
        Cache newCache = new Cache();
        return !_caches.TryAdd(pluginIdentifier, newCache) ? throw new InvalidOperationException("Cache already exists") : newCache;
    }
}

public class Cache : IPluginCache
{
    private ConcurrentDictionary<string, object> _data = new ConcurrentDictionary<string, object>();
    
    public T? Get<T>(string key)
    {
        return (T?)_data[key];
    }

    public void Register<T>(string key, CachedData<T> data) where T : class
    {
        _data.TryAdd(key, data);
        _ = AddCacheDaemonAsync(data);
    }

    private async Task AddCacheDaemonAsync<T>(CachedData<T> data) where T : class
    {
        while (true)
        {
            await Task.Delay(data.InvalidationFrequency);
            data.UpdateData();
        }
    }
}