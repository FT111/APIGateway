using System.ComponentModel.Design;
using GatewayPluginContract;
using GatewayPluginContract.Entities;

namespace Gateway;

// Nain director
public class Gateway(StoreFactory store, SupervisorClient supervisor, TaskQueue taskQueue, IConfigurationsProvider configurationsProvider, PluginManager pluginManager)
{
    public StoreFactory Store { get; } = store;
    public SupervisorClient Supervisor { get; } = supervisor;
    public TaskQueue TaskQueue { get; set; } = taskQueue;
    public IConfigurationsProvider ConfigurationsProvider { get; set; } = configurationsProvider;
    public PluginManager PluginManager { get; set; } = pluginManager;
    public RequestPipeline Pipe { get; set; } = new RequestPipelineBuilder().
        WithConfigProvider(configurationsProvider)
        .WithRepoProvider(store.CreateStore().GetRepoFactory())
        .WithBackgroundQueue(taskQueue)
        .Build();
    
    
}

public class GatewayBuilder
{
    private StoreFactory StoreFactory { get; set; } = null!;
    private SupervisorClient Supervisor { get; set; } = null!;
    private TaskQueue TaskQueue { get; set; } = null!;
    private IConfigurationsProvider ConfigurationsProvider { get; set; } = null!;
    private PluginManager PluginManager { get; set; } = new PluginManager();
    
    public async Task<Gateway> Build()
    {
        await Supervisor.StartAsync();
        return await Task.FromResult(new Gateway(StoreFactory, Supervisor, TaskQueue, ConfigurationsProvider, PluginManager));
    }
    public void WithStoreProvider(StoreFactory storeFactory)
    {
        StoreFactory = storeFactory;
    }

    public void WithSupervisor(SupervisorAdapter supervisorAdapter, IConfiguration configuration)
    {
        Supervisor = new SupervisorClient(supervisorAdapter, PluginManager, configuration)
            ?? throw new ArgumentNullException(nameof(supervisorAdapter));
    }
    
    public void WithTaskQueue(TaskQueue taskQueue)
    {
        TaskQueue = taskQueue ?? throw new ArgumentNullException(nameof(taskQueue));
    }
    
    public void WithConfigurationsProvider(IConfigurationsProvider configurationsProvider)
    {
        ConfigurationsProvider = configurationsProvider ?? throw new ArgumentNullException(nameof(configurationsProvider));
    }
    
    public async Task LoadPluginServicesAsync(string pluginDirectory)
    {
        await PluginManager.LoadPluginsAsync(pluginDirectory);
    }

    public async Task<Gateway> BuildFromConfiguration(IConfiguration configuration)
    {
        await PluginManager.LoadPluginsAsync(configuration["PluginDirectory"] ?? "service/plugins");
        var coreServices = configuration.GetSection("coreServices") ?? throw new InvalidOperationException("coreServices configuration section not found.");
        var requiredServices = new[] {"Store", "Supervisor", "TaskQueue", "ConfigurationsProvider"};
        Dictionary<string, string> serviceIdentifiers = new Dictionary<string, string>();
        
        foreach (var service in requiredServices)
        {
            serviceIdentifiers.Add(service, coreServices[service] ?? throw new InvalidOperationException($"Service '{service}' not found in configuration."));
        }
        
        StoreFactory = PluginManager.Registrar.GetServiceByName<StoreFactory>(serviceIdentifiers["Store"]).Instance
            ?? throw new InvalidOperationException("StoreFactory service not found.");
        Supervisor = new SupervisorClient(PluginManager.Registrar.GetServiceByName<SupervisorAdapter>(serviceIdentifiers["Supervisor"]).Instance, PluginManager, configuration)
            ?? throw new InvalidOperationException("SupervisorClient service not found.");
        TaskQueue = PluginManager.Registrar.GetServiceByName<TaskQueue>(serviceIdentifiers["TaskQueue"]).Instance
            ?? throw new InvalidOperationException("TaskQueue service not found.");
        ConfigurationsProvider = PluginManager.Registrar.GetServiceByName<IConfigurationsProvider>(serviceIdentifiers["ConfigurationsProvider"]).Instance
            ?? throw new InvalidOperationException("ConfigurationsProvider service not found.");
        
        return await Build();
    }
}