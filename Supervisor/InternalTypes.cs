using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;

namespace Supervisor;

public static class InternalTypes
{
    public static class Repositories
    {
        public class InternalRepositoryProviderWrapper(GatewayPluginContract.Repositories repositories)
        {
            public IDataRepository<T> GetRepo<T>() where T : class
            {
                return repositories.GetRepo<T>();
            }
            
            public DbContext Context => repositories.Context;
        }
        
        public class Gateway(GatewayPluginContract.Repositories repositories) : InternalRepositoryProviderWrapper(repositories);
        public class Supervisor(GatewayPluginContract.Repositories repositories) : InternalRepositoryProviderWrapper(repositories);

    }
    // Marker interface for DI
}
