using System.IO.Pipelines;

namespace GatewayPluginContract;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;


public class PipeConfiguration
{
    public required List<PipeProcessorContainer> PreProcessors { get; set; } 
    public required List<PipeProcessorContainer> PostProcessors { get; set; }
    public required  GatewayPluginContract.IRequestForwarder? Forwarder { get; set; }
}

public class PipeConfigurationRecipe
{
    public required List<string> ServiceList { get; set; }
}

public class PipeProcessorContainer
{
    public GatewayPluginContract.IRequestProcessor Processor { get; set; } = null!;
    public uint Order { get; set; } = 0;
    public bool IsEnabled { get; set; } = true;
    public string Identifier { get; set; } = string.Empty;
}

public interface IStore
{
    /// <summary>
    /// Interface for a simple key-value store.
    /// Implementations should provide a persistent storage mechanism.
    /// </summary>
    Task<T> GetAsync<T>(string key, string? scope = null) where T : notnull;
    Task SetAsync<T>(string key, T value, string type, string? scope = null) where T : notnull;
    Task RemoveAsync(string key, string? scope = null);
    
    Task<Dictionary<string, Dictionary<string, string>>> GetPluginConfigsAsync(string? endpoint = null);
    
    Task<PipeConfigurationRecipe> GetPipeConfigRecipeAsync(string? endpoint = null);
    
    
}

public interface IScopedStore
{
    Task<T> GetAsync<T>(string key) where T : notnull;
    Task SetAsync<T>(string key, string type, T value) where T : notnull;
    Task RemoveAsync(string key);
}


public class PluginManifest
{
    public required string Name { get; init; }
    public required string Version { get; init; } 
    public required string Description { get; init; }
    public required string Author { get; init; }
}

public interface IRequestContext
{
    HttpRequest Request { get; set; }
    HttpResponse Response { get; set; }
    bool IsBlocked { get; set; }
    bool IsRestartRequested { get; set; }
    bool IsForwardingFailed { get; set; }
    uint RestartCount { get; set; }
    string TargetPathBase { get; set; }
    string GatewayPathPrefix { get; set; }
    Dictionary<string, Dictionary<string, string>> PluginConfiguration { get; set; }
}

public class RequestContext : IRequestContext
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
}

public interface IBackgroundQueue
{
    void QueueTask(Func<CancellationToken, ValueTask> task);
    Task<Func<CancellationToken, ValueTask>> DequeueAsync(CancellationToken cancellationToken = default);
}

public interface IService
{
    // Marker interface for services
}

public interface IRequestProcessor : IService
{
    // With request context and a method to add a deferred task
    Task ProcessAsync(IRequestContext context, IBackgroundQueue backgroundQueue, IScopedStore store);
}

public interface IRequestForwarder : IService
{
    Task ForwardAsync(IRequestContext context);
}

public enum ServiceTypes
{
    PreProcessor,
    PostProcessor,
    Forwarder
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