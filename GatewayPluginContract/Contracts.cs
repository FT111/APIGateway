using System.IO.Pipelines;
using System.Linq.Expressions;
using GatewayPluginContract.Entities;
using Microsoft.Extensions.Configuration;

namespace GatewayPluginContract;
using DeferredFunc = Func<CancellationToken, IRepositories, Task>;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

public class PipeConfiguration
{
    public required List<PipeProcessorContainer> PreProcessors { get; set; } 
    public required List<PipeProcessorContainer> PostProcessors { get; set; }
    public required  GatewayPluginContract.IRequestForwarder? Forwarder { get; set; }
    public Endpoint? Endpoint { get; init; }
}

// public class PipeRecipeServiceContainer
// {
//     public required string Identifier { get; set; }
//     public required ServiceFailurePolicies FailurePolicy { get; set; }
// }

// public class PipeConfigurationRecipe
// {
//     public required List<PipeRecipeServiceContainer> ServiceList { get; set; }
// }


public class ServiceContainer<T> where T : IService

{
    public required T Instance { get; set; }
    public required ServiceTypes ServiceType { get; set; }
    public required ServiceIdentity Identity { get; set; }
    // public required Dictionary<string, string> PluginConfiguration { get; set; }
}


public class PipeProcessorContainer
{
    public ServiceContainer<IRequestProcessor> Processor { get; set; } = null!;
    public uint Order { get; set; } = 0;
    public bool IsEnabled { get; set; } = true;
    public ServiceFailurePolicies FailurePolicy { get; set; } = ServiceFailurePolicies.Ignore;
    public string Identifier { get; set; } = string.Empty;
}

public interface IDataRepository<T> where T : class
{
    Task<T?> GetAsync(params object[] key);
    Task<List<T>> GetAllAsync();
    Task AddAsync(T model);
    Task RemoveAsync(string key);
    Task UpdateAsync(T model);
    Task<IEnumerable<T>> QueryAsync(Expression<Func<T, bool>> predicate);
}

public interface IRepositories
{
    IDataRepository<T> GetRepo<T>() where T : class;
}

public abstract class Store(IConfiguration configuration)
{
    public abstract IRepositories GetRepoFactory();
}


public class PluginManifest
{
    public required string Name { get; init; }
    public required double Version { get; init; } 
    public required string Description { get; init; }
    public required string Author { get; init; }
    public List<PluginDependency> Dependencies { get; init; } = [];
}

public class PluginDependency
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required Func<double, bool> VersionCheck { get; init; }
    public bool IsOptional { get; init; } = false;
    public bool IsProvided { get; set; } = false;
}

public class RequestContext
{
    public required HttpRequest Request { get; set; }
    public required HttpResponse Response { get; set; }
    public bool IsBlocked { get; set; } = false;
    public bool IsRestartRequested { get; set; } = false;
    public bool IsForwardingFailed { get; set; } = false;
    public uint RestartCount { get; set; } = 0;
    // public required string TargetPathBase { get; set; }
    public Endpoint? Endpoint { get; set; } = null!;
    public Target Target { get; set; } = null!;

    public required Request LogRequest { get; set; }
    public string GatewayPathPrefix { get; set; } = string.Empty;
    public Dictionary<string, Dictionary<string, string>> PluginConfiguration { get; set; } = new();
    public Dictionary<string, Dictionary<string, string>> SharedPluginContext { get; set; } = new();
}


public interface IBackgroundQueue : IService
{
    void QueueTask(DeferredFunc task);
    Task<DeferredFunc> DequeueAsync(CancellationToken cancellationToken = default);
}


public class ServiceContext
{
    public required IBackgroundQueue DeferredTasks { get; init; } 
    public required IRepositories DataRepositories { get; init; }
    public required ServiceIdentity Identity { get; init; }
}

public class ServiceIdentity
{
    public required string Identifier { get; init; }
    public required PluginManifest OriginManifest { get; init; }
}

public interface IService
{
    // Marker interface for services
}

public interface IRequestProcessor : IService
{
    // With request context and a method to add a deferred task
    Task ProcessAsync(RequestContext context, ServiceContext services);
}

public interface IRequestForwarder : IService
{
    Task ForwardAsync(RequestContext context);
}

public enum ServiceTypes
{
    PreProcessor,
    PostProcessor,
    Forwarder,
    Core
}

public enum ServiceFailurePolicies
{
    Ignore,
    RetryThenBlock,
    RetryThenIgnore,
    Block
}

public interface IPluginServiceRegistrar
{
    void RegisterService<T>(IPlugin parentPlugin, T service, ServiceTypes serviceType) where T : IService;
}

public interface IDataRegistrar
{
    void RegisterDataCard<T>(DataCard<T> card) where T : class, Visualisation.ICardVisualisation;
}

public class DataCard<TModel> where TModel : class, Visualisation.ICardVisualisation
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required Func<IRepositories, TModel> GetData { get; init; }
}

public interface IPlugin
{
    public PluginManifest GetManifest();
    
    public Dictionary<ServiceTypes, IService[]> GetServices();

    public void ConfigurePluginRegistrar(IPluginServiceRegistrar registrar);
    
    public void ConfigureDataRegistrar(IDataRegistrar registrar);
}

public abstract class StoreFactory(IConfiguration configuration) : IService
{
    public abstract Store CreateStore();
}

public abstract class SupervisorAdapter(IConfiguration configuration) : IService
{
    public abstract Task SendEventAsync(SupervisorEvent eventData);
    public abstract Task SubscribeAsync(SupervisorEventType eventType, Func<SupervisorEvent, Task> handler);
}

public enum SupervisorEventType
{
    Command,
    Event,
    Heartbeat
}