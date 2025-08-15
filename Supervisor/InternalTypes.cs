using GatewayPluginContract;
using GatewayPluginContract.Entities;

namespace Supervisor;

public static class InternalTypes
{
    public static class Repositories
    {
        public abstract class InternalRepositoryProviderWrapper(IRepositories repositories)
        {
            public IDataRepository<T> GetRepo<T>() where T : Entity
            {
                return repositories.GetRepo<T>();
            }
        }
        
        public class Gateway(IRepositories repositories) : InternalRepositoryProviderWrapper(repositories);
        public class Supervisor(IRepositories repositories) : InternalRepositoryProviderWrapper(repositories);

    }
    // Marker interface for DI
}
