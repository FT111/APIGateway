using System.IO.Pipelines;
using Microsoft.Extensions.Configuration;

namespace GatewayPluginContract;
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

public class PipeRecipeServiceContainer
{
    public required string Identifier { get; set; }
    public required ServiceFailurePolicies FailurePolicy { get; set; }
}

public class PipeConfigurationRecipe
{
    public required List<PipeRecipeServiceContainer> ServiceList { get; set; }
}

public class PipeProcessorContainer
{
    public GatewayPluginContract.IRequestProcessor Processor { get; set; } = null!;
    public uint Order { get; set; } = 0;
    public bool IsEnabled { get; set; } = true;
    public ServiceFailurePolicies FailurePolicy { get; set; } = ServiceFailurePolicies.Ignore;
    public string Identifier { get; set; } = string.Empty;
}

public interface IDataRepository<T> where T : GatewayModel
{
    Task<T?> GetAsync(string key);
    Task AddAsync(T model);
    Task RemoveAsync(string key);
    Task UpdateAsync(T model);
}

public interface IRepoFactory
{
    IDataRepository<T> GetRepo<T>() where T : GatewayModel;
}

public abstract class Store(IConfiguration configuration)
{
    public abstract IRepoFactory GetRepoFactory();
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
    public required string TargetPathBase { get; set; }
    public string GatewayPathPrefix { get; set; } = string.Empty;
    public Dictionary<string, Dictionary<string, string>> PluginConfiguration { get; set; } = new();
    public Dictionary<string, Dictionary<string, string>> SharedPluginContext { get; set; } = new();
}

public interface IBackgroundQueue
{
    void QueueTask(Func<CancellationToken, ValueTask> task);
    Task<Func<CancellationToken, ValueTask>> DequeueAsync(CancellationToken cancellationToken = default);
}

public class Event
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required bool IsWarning { get; init; } = false;
    public required Endpoint Endpoint { get; init; }
    public required string ServiceIdentifier { get; init; }
    public required string Type { get; init; }
    public string? Data { get; init; }
}

public interface IEvents
{
    public void RegisterEvent(Event eventData);
}

public class ServiceToolkit
{
    public required IBackgroundQueue BackgroundQueue { get; init; }
    public required IEvents Events { get; init; }
}

public interface IService
{
    // Marker interface for services
}

public interface IRequestProcessor : IService
{
    // With request context and a method to add a deferred task
    Task ProcessAsync(RequestContext context, ServiceToolkit services);
}

public interface IRequestForwarder : IService
{
    Task ForwardAsync(RequestContext context);
}

public enum ServiceTypes
{
    PreProcessor,
    PostProcessor,
    Forwarder
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

public interface IPlugin
{
    public PluginManifest GetManifest();
    
    public Dictionary<ServiceTypes, IService[]> GetServices();

    public void ConfigureRegistrar(IPluginServiceRegistrar registrar);
}