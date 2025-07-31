using GatewayPluginContract;
namespace Gateway;

// public static class Store
// {
//     private class ScopedStore(IRepoFactory repoFactory, string scope) : IScopedStore
//     {
//         public async Task<T> GetAsync<T>(string key) where T : notnull
//         {
//             return await repoFactory.GetAsync<T>(key, scope);
//         }
//         
//         public async Task SetAsync<T>(string key, string type, T value) where T : notnull
//         {
//             await repoFactory.SetAsync(key, value, type, scope);
//         }
//         
//         public async Task RemoveAsync(string key)
//         {
//             await repoFactory.RemoveAsync(key, scope);
//         }
//     }
//     public static IScopedStore CreateScopedStore(IRepoFactory repoFactory, string scope)
//     {
//         return new ScopedStore(repoFactory, scope);
//     }
// }