using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq.Expressions;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Serialization;
using GatewayPluginContract;
using GatewayPluginContract.Entities;
using GatewayPluginContract.MQ;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;

namespace GatewayPluginContract;
using DeferredFunc = Func<CancellationToken, Repositories, Task>;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

public class PipeConfiguration
{
    public required List<PipeProcessorContainer> PreProcessors { get; set; } 
    public required List<PipeProcessorContainer> PostProcessors { get; set; }
    public required  GatewayPluginContract.IRequestForwarder? Forwarder { get; set; } 
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

public abstract class Repositories
{
    public abstract required DbContext Context { get; init; }
    public abstract IDataRepository<T> GetRepo<T>() where T : class;
}

public abstract class Store
{
    public abstract Repositories GetRepoFactory();
    public abstract required DbContext Context { get; init; }
}


public class PluginManifest
{
    public required string Name { get; init; }
    public required double Version { get; init; } 
    public required string Description { get; init; }
    public required string Author { get; init; }
    public List<MqCommandSubmission> Commands { get; init; } = [];
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
    public required ILogger Logger { get; init; }
    public Activity TraceActivity { get; set; } = Activity.Current ?? new Activity("Gateway Pipeline");
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
    public RouteNode? Route { get; set; }
}

public class RouteNode
{
    public string Segment { get; set; } = null!;
    public Dictionary<string, RouteNode> Children { get; set; } = new Dictionary<string, RouteNode>();
    public Endpoint? Endpoint { get; set; } = null;
    public Dictionary<string, Dictionary<string, string>> RoutedPluginConfs { get; set; } = new Dictionary<string, Dictionary<string, string>>();
    public Target? Target;
}


public interface IBackgroundQueue : IService
{
    void QueueTask(Func<CancellationToken, Repositories, Activity, ILogger, Task> task);
    Task<Func<CancellationToken, Repositories, Activity, ILogger, Task>> DequeueAsync(CancellationToken cancellationToken);
}


public class ServiceContext
{
    public required IBackgroundQueue DeferredTasks { get; init; } 
    public required Repositories DataRepositories { get; init; }
    public required ServiceIdentity Identity { get; init; }
    public required PluginCache Cache { get; init; }
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

public interface IPresetConfigValuePrompts
{ 
    ICollection<string> Targets(DbContext context);
    ICollection<string> Endpoints(DbContext context);
    ICollection<string> Deployments(DbContext context);
    ICollection<string> Schemas(DbContext context);
    ICollection<string> Pipes(DbContext context);
}

public interface IPluginServiceRegistrar
{
    void RegisterService<T>(IPlugin parentPlugin, T service, ServiceTypes serviceType) where T : IService;
    void RegisterServiceWithTypeDef(Type serviceInstanceType, IPlugin parentPlugin, object serviceInstance, ServiceTypes serviceType);
    IEnumerable<T> GetServicesByType<T>(ServiceTypes serviceType) where T : IService;
    ServiceContainer<T> GetServiceByName<T>(string name) where T : IService;
    void RegisterInternalServiceWithRuntimeType(Type serviceInstanceType, object serviceInstance, string identifier);
    void Reset();
}

public interface ITelemetryRegistrar
{
    void RegisterDataCard<T>(DataCard<T> card) where T : class, Visualisation.ICardVisualisation;
    
    // void RegisterConfigConstraint(PluginConfigConstraint constraint);
}

public class PluginConfigDefinition
{
    public bool Internal { get; set; }
    public string Key { get; set; }
    public required string PluginNamespace { get; init; }
    public string DefaultValue { get; set; } = "";
    public string ValueType { get; set; } = "string";
    public string ConstraintDescription { get; set; } = "";
    public Func<DbContext,  List<string>>? ValuePrompts;
    public Predicate<string>? ValueConstraint { get; set; }
}

public class MqCommandSubmission
{
    public required string Identifier { get; init; }
    public required Func<GatewayBase, string?, Task> Handler { get; init; }
    public Task HandleAsync(GatewayBase gateway, string? param) => Handler(gateway, param);

}

public class DataCard<TModel> where TModel : class, Visualisation.ICardVisualisation
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required Func<Repositories, TModel> GetData { get; init; }
}

public abstract class PluginCacheManager(StoreFactory stf)
{
    public abstract PluginCache GetCache(string pluginIdentifier);
}

public abstract class PluginCache(StoreFactory ctx, ICacheHandler ch)
{
    public abstract T? Get<T>(string key) where T : class;
    public abstract Task Register<T>(string key, CachedData<T> data) where T : class;
}

public interface ICacheHandler
{}

public class CachedData<T> where T : class
{
    private T _data;

    public T Data { get => Volatile.Read(ref _data);
        set => Interlocked.Exchange(ref _data, value);
    }
    public required Func<DbContext, Task<T>> Fetch { get; set; }
    public TimeSpan? InvalidationFrequency { get; init; }
}

public interface IPlugin
{
    public PluginManifest GetManifest();
    
    public Dictionary<ServiceTypes, IService[]> GetServices();

    public void ConfigurePluginRegistrar(IPluginServiceRegistrar registrar);
    
    public void ConfigureDataRegistries(PluginCache cache);
    
    public void InitialiseServiceConfiguration(DbContext context, Func<Func<PluginConfigDefinition, PluginConfigDefinition>, Task> addConfig);
}

public abstract class StoreFactory(IConfiguration configuration) : IService
{
    public abstract Store CreateStore();
}

public abstract class SupervisorAdapter(IConfiguration configuration) : IService
{
    public abstract Task SendEventAsync(SupervisorEvent eventData, Guid? targetInstanceId = null, Guid? correlationId = null);
    // public abstract Task<SupervisorEvent> AwaitResponseAsync(Guid correlationId, TimeSpan timeout);
    public abstract Task SubscribeAsync(SupervisorEventType eventType, Func<SupervisorEvent, Task> handler, Guid? instanceId = null, Guid? correlationId = null);
}

public enum SupervisorEventType
{
    Command,
    Event
}

public class RequestPipelineBase
{
    public IRouteTrie Router;
}

public interface IRouteTrie
{
    void Insert(string path, Endpoint endpoint, Dictionary<string, Dictionary<string, string>> collatedPluginConfigs);
    RouteNode? FindClosest(string path);
}

public static class DefaultMqCommands
{
    public static readonly Contracts.MqCommandKey Restart = Contracts.MqCommandKey.Internal("restart");
    public static readonly Contracts.MqCommandKey ApplyBufferedRoutes = Contracts.MqCommandKey.Internal("routes.apply-preload");
    public static readonly Contracts.MqCommandKey UpdateRoutes = Contracts.MqCommandKey.Internal("routes.update");
    public static readonly Contracts.MqCommandKey UpdatePlugins = Contracts.MqCommandKey.Internal("plugins.update");
    public static readonly Contracts.MqCommandKey Stop = Contracts.MqCommandKey.Internal("stop");
    public static readonly Contracts.MqCommandKey PreloadRoutes = Contracts.MqCommandKey.Internal("routes.preload");
    public static readonly Contracts.MqCommandKey Heartbeat = Contracts.MqCommandKey.Internal("heartbeat");
    public static readonly Contracts.MqCommandKey UpdateDeliveryUrl = Contracts.MqCommandKey.Internal("packages.url.update");
    public static readonly Contracts.MqCommandKey Response = Contracts.MqCommandKey.Internal("response");
    
}

public interface IPluginPackageManager : IService
{
    public string GetPluginStaticUrl();
    public void PackagePluginsAsync();
}

public interface IPluginManager
{
    public List<IPlugin> Plugins { get; }
    public Task LoadPluginsAsync(string path);
    public void AddPluginLoadStep(Func<IPlugin, Task> step);
    public List<IPlugin> GetPlugins { get; }

    public Task<PluginVerificationResult> VerifyInstalledPluginsAsync(
        IQueryable<PipeService> services);
    
    public Task DownloadAndInstallPluginAsync(string identifier);

    public Task RemovePluginAsync(string identifier);

    public IPluginServiceRegistrar Registrar { get; }

}

public interface IPLuginInitialiser
{
    void InitialiseFromPluginManager(IPluginManager manager);
    void InitialisePluginIfUninitialised(IPlugin plugin);
}

public class PluginVerificationResult
{
    public List<string> Missing { get; set; } = [];
    public List<string> Removed { get; set; } = [];
    public bool IsValid => Missing.Count == 0 && Removed.Count == 0;
}




public abstract class GatewayBase
{
    public IConfiguration BaseConfiguration { get; }
    public StoreFactory Store { get; }
    // Use the contract interface for background queue
    public IBackgroundQueue LocalTaskQueue { get; set; }
    public ILogger? Logger { get; set; }
    public IPluginManager PluginManager { get; set; } = null!;
    public IPLuginInitialiser PluginInitManager { get; set;  }
    public RequestPipelineBase Pipe { get; set; } = null!;
    public abstract Task<IRouteTrie> CreateRouterAsync();
    
    

    protected GatewayBase(IConfiguration configuration, StoreFactory store, IBackgroundQueue localTaskQueue, IPluginManager pluginManager, RequestPipelineBase requestPipeline)
    {
        BaseConfiguration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        Store = store ?? throw new ArgumentNullException(nameof(store));
        LocalTaskQueue = localTaskQueue ?? throw new ArgumentNullException(nameof(localTaskQueue));
        PluginManager = pluginManager ?? throw new ArgumentNullException(nameof(pluginManager));
        Pipe = requestPipeline ?? throw new ArgumentNullException(nameof(requestPipeline));
    }

    // Small helper so concrete implementations can extend logger behaviour
    public virtual void AddLogger(ILogger logger) => Logger = logger;
}
