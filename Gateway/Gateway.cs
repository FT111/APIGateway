using System.ComponentModel.Design;
using Gateway.services;
using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;
using SharedServices;

namespace Gateway;

// Main director
public class Gateway(IConfiguration configuration, StoreFactory store, LocalTaskQueue localTaskQueue, IConfigurationsProvider configurationsProvider, PluginManager pluginManager, 
    Identity.Identity identity, PluginInitialisation.PluginConfigManager pluginInitManager, CacheManager cacheManager)
{
    public IConfiguration BaseConfiguration { get; } = configuration ?? throw new ArgumentNullException(nameof(configuration));
    public StoreFactory Store { get; } = store;
    public LocalTaskQueue LocalTaskQueue { get; set; } = localTaskQueue;
    public IConfigurationsProvider ConfigurationsProvider { get; set; } = configurationsProvider;
    public PluginManager PluginManager { get; set; } = pluginManager;
    public CacheManager CacheManager { get; set; } = cacheManager;
    public Identity.Identity Identity { get; init; } = identity;
    public PluginInitialisation.PluginConfigManager PluginInitManager { get; init; } = pluginInitManager!;
    public ILogger? Logger { get; set; }
    public RouteTrie? BufferedRouter { get; set; }

    internal Func<string, Func<SupervisorEvent, Task>, Task> AddCustomSupervisorHandler = null!;
    internal Func<SupervisorEvent, Guid?, Guid?, Task> SendSupervisorEvent = null!;

    public RequestPipeline Pipe { get; set; } = new RequestPipelineBuilder()
        .WithConfigProvider(configurationsProvider)
        .WithRepoProvider(store.CreateStore().GetRepoFactory())
        .WithBackgroundQueue(localTaskQueue)
        .WithCacheProvider(cacheManager)
        .WithRouterFactory(RouterFactory.BuildRouteTrie)
        .WithIdentity(identity)
        .Build();

    public TaskQueueHandler TaskQueueHandler { get; set; } = new TaskQueueHandler(store, localTaskQueue);
    
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await TaskQueueHandler.ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task<RouteTrie> CreateRouterAsync()
    {
        return await RouterFactory.BuildRouteTrie(store.CreateStore().Context, ConfigurationsProvider);
    }
    
    public void AddLogger(ILogger logger)
    {
        Logger = logger;
        TaskQueueHandler.AddLogger(logger);
    }
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
    private CacheManager CacheManager { get; set; } = null!;
    private PluginInitialisation.PluginConfigManager PluginInitManager { get; set; } = null!;
    private CommandManager CommandManager { get; set; } = null!;
    private PluginManager PluginManager { get; set; } = new PluginManager(configuration);
    
    public async Task<GatewayBuild> Build()
    {
        var identity = new Identity.Identity(_configuration);
        var gateway = new Gateway(_configuration, StoreFactory, LocalTaskQueue, ConfigurationsProvider,
            PluginManager, identity, PluginInitManager, CacheManager);
        gateway.StartAsync();
        var supervisorClient = new SupervisorClient(SupervisorAdapter, gateway)
            ?? throw new ArgumentNullException(nameof(SupervisorAdapter));
            
        await supervisorClient.StartAsync();
        
        var context = StoreFactory.CreateStore().Context;
        var instance = await context.Set<Instance>().FindAsync(identity.Id);
        if (instance == null)
        {
            instance = (Instance)identity;
            var repo = context.Set<Instance>();
            await repo.AddAsync(instance);
        }
        else
        {
            instance.Status = "online";
        }
        await context.SaveChangesAsync();

        
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
    }

    public async Task LoadPluginServicesAsync(string pluginDirectory)
    {
        await PluginManager.LoadPluginsAsync(pluginDirectory);
    }
    
    public void SetupPluginManager()
    {
        CacheManager.ConfigurePluginManager(PluginManager);
        CommandManager.ConfigurePluginManager(PluginManager);
    }

    public async Task<GatewayBuild> BuildFromConfiguration()
    {
        PluginManager.LoadInternalServices(configuration["CoreServicesNamespace"] ?? "Gateway.services");
        var coreServices = configuration.GetSection("coreServices") ?? throw new InvalidOperationException("coreServices configuration section not found.");
        SetupBaseComponents(coreServices);

        SetupDependentComponents();

        SetupPluginManager();
        
        await PluginManager.LoadPluginsAsync(configuration["PluginDirectory"] ?? "service/plugins");
        return await Build();
    }

    public void SetupDependentComponents()
    {
        ConfigurationsProvider = new ConfigProvider(configuration, PluginManager);
        PluginInitManager = new PluginInitialisation.PluginConfigManager(StoreFactory.CreateStore().Context, PluginManager);
        CacheManager = new CacheManager(StoreFactory);
        CommandManager = new CommandManager();
    }

    public void SetupBaseComponents(IConfigurationSection coreServices)
    {
        var baseComponents = new[] {"Store", "Supervisor", "TaskQueue", "ConfigurationProvider"};
        Dictionary<string, string> serviceIdentifiers = new Dictionary<string, string>();
        
        foreach (var service in baseComponents)
        {
            serviceIdentifiers.Add(service, coreServices[service] ?? throw new InvalidOperationException($"Service '{service}' not found in configuration."));
        }
        
        StoreFactory = PluginManager.Registrar.GetServiceByName<StoreFactory>(serviceIdentifiers["Store"]).Instance
                       ?? throw new InvalidOperationException("StoreFactory service not found.");
        SupervisorAdapter = PluginManager.Registrar.GetServiceByName<SupervisorAdapter>(serviceIdentifiers["Supervisor"]).Instance
                            ?? throw new InvalidOperationException("SupervisorClient service not found.");
        LocalTaskQueue = PluginManager.Registrar.GetServiceByName<LocalTaskQueue>(serviceIdentifiers["TaskQueue"]).Instance
                         ?? throw new InvalidOperationException("TaskQueue service not found.");
    }
}
