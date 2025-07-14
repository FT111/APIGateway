using GatewayPluginContract;
namespace Gateway;

public static class Store
{
    private class ScopedStore(IStore store, string scope) : IScopedStore
    {
        public async Task<T> GetAsync<T>(string key) where T : notnull
        {
            return await store.GetAsync<T>(key, scope);
        }
        
        public async Task SetAsync<T>(string key, string type, T value) where T : notnull
        {
            await store.SetAsync(key, value, type, scope);
        }
        
        public async Task RemoveAsync(string key)
        {
            await store.RemoveAsync(key, scope);
        }
    }
    public static IScopedStore CreateScopedStore(IStore store, string scope)
    {
        return new ScopedStore(store, scope);
    }
}