using System.ComponentModel.Design;
using Gateway.services;
using GatewayPluginContract;
using GatewayPluginContract.Entities;

namespace Gateway;

// Nain director
public class Gateway(IConfiguration configuration, StoreFactory store, LocalTaskQueue localTaskQueue, IConfigurationsProvider configurationsProvider, PluginManager pluginManager)
{
    public IConfiguration BaseConfiguration { get; } = configuration ?? throw new ArgumentNullException(nameof(configuration));
    public StoreFactory Store { get; } = store;
    public LocalTaskQueue LocalTaskQueue { get; set; } = localTaskQueue;
    public IConfigurationsProvider ConfigurationsProvider { get; set; } = configurationsProvider;
    public PluginManager PluginManager { get; set; } = pluginManager;
    public RequestPipeline Pipe { get; set; } = new RequestPipelineBuilder().
        WithConfigProvider(configurationsProvider)
        .WithRepoProvider(store.CreateStore().GetRepoFactory())
        .WithBackgroundQueue(localTaskQueue)
        .Build();
}

public class GatewayBuild(Gateway gateway, SupervisorClient supervisorClient)
{
    public Gateway Gateway { get; } = gateway;
    public SupervisorClient SupervisorClient { get; } = supervisorClient;
}

public class GatewayBuilder(IConfiguration configuration)
{
    private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private StoreFactory StoreFactory { get; set; } = null!;
    private SupervisorAdapter SupervisorAdapter { get; set; } = null!;
    private LocalTaskQueue LocalTaskQueue { get; set; } = null!;
    private IConfigurationsProvider ConfigurationsProvider { get; set; } = null!;
    private PluginManager PluginManager { get; set; } = new PluginManager(configuration);
    
    public async Task<GatewayBuild> Build()
    {
        var gateway = new Gateway(_configuration, StoreFactory, LocalTaskQueue, ConfigurationsProvider, PluginManager);
        var supervisorClient = new SupervisorClient(SupervisorAdapter, gateway)
            ?? throw new ArgumentNullException(nameof(SupervisorAdapter));
            
        await supervisorClient.StartAsync();
        
        gateway.ConfigurationsProvider.LoadPipeConfigs();
        gateway.ConfigurationsProvider.LoadServiceConfigs();
        
        return new GatewayBuild(
            gateway, 
            supervisorClient
        );
    }
    public void WithStoreProvider(StoreFactory storeFactory)
    {
        StoreFactory = storeFactory;
    }

    public void WithSupervisor(SupervisorAdapter supervisorAdapter)
    {
        SupervisorAdapter = supervisorAdapter
            ?? throw new ArgumentNullException(nameof(supervisorAdapter));
    }
    
    public void WithTaskQueue(LocalTaskQueue localTaskQueue)
    {
        LocalTaskQueue = localTaskQueue ?? throw new ArgumentNullException(nameof(localTaskQueue));
    }
    
    public async Task WithConfigurationsProvider(IConfigurationsProvider configurationsProvider)
    {
        ConfigurationsProvider = configurationsProvider ?? throw new ArgumentNullException(nameof(configurationsProvider));
        await ConfigurationsProvider.InitialiseAsync(PluginManager, StoreFactory.CreateStore().GetRepoFactory());
    }
    
    public async Task LoadPluginServicesAsync(string pluginDirectory)
    {
        await PluginManager.LoadPluginsAsync(pluginDirectory);
    }

    public async Task<GatewayBuild> BuildFromConfiguration()
    {
        await PluginManager.LoadPluginsAsync(configuration["PluginDirectory"] ?? "service/plugins");
        PluginManager.LoadInternalServices(configuration["CoreServicesNamespace"] ?? "Gateway.services");
        var coreServices = configuration.GetSection("coreServices") ?? throw new InvalidOperationException("coreServices configuration section not found.");
        var requiredServices = new[] {"Store", "Supervisor", "TaskQueue", "ConfigurationProvider"};
        Dictionary<string, string> serviceIdentifiers = new Dictionary<string, string>();
        
        foreach (var service in requiredServices)
        {
            serviceIdentifiers.Add(service, coreServices[service] ?? throw new InvalidOperationException($"Service '{service}' not found in configuration."));
        }
        
        StoreFactory = PluginManager.Registrar.GetServiceByName<StoreFactory>(serviceIdentifiers["Store"]).Instance
            ?? throw new InvalidOperationException("StoreFactory service not found.");
        SupervisorAdapter = PluginManager.Registrar.GetServiceByName<SupervisorAdapter>(serviceIdentifiers["Supervisor"]).Instance
            ?? throw new InvalidOperationException("SupervisorClient service not found.");
        LocalTaskQueue = PluginManager.Registrar.GetServiceByName<LocalTaskQueue>(serviceIdentifiers["TaskQueue"]).Instance
            ?? throw new InvalidOperationException("TaskQueue service not found.");
        ConfigurationsProvider = PluginManager.Registrar.GetServiceByName<IConfigurationsProvider>(serviceIdentifiers["ConfigurationProvider"]).Instance
            ?? throw new InvalidOperationException("ConfigurationProvider service not found.");
        await ConfigurationsProvider.InitialiseAsync(PluginManager, StoreFactory.CreateStore().GetRepoFactory());
        
        return await Build();
    }
}