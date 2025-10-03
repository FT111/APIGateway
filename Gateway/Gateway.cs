using System.ComponentModel.Design;
using Gateway.services;
using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gateway;

// Main director
public class Gateway(IConfiguration configuration, StoreFactory store, LocalTaskQueue localTaskQueue, IConfigurationsProvider configurationsProvider, PluginManager pluginManager, Identity.Identity identity, RouteTrie router)
{
    public IConfiguration BaseConfiguration { get; } = configuration ?? throw new ArgumentNullException(nameof(configuration));
    public StoreFactory Store { get; } = store;
    public LocalTaskQueue LocalTaskQueue { get; set; } = localTaskQueue;
    public IConfigurationsProvider ConfigurationsProvider { get; set; } = configurationsProvider;
    public PluginManager PluginManager { get; set; } = pluginManager;
    public Identity.Identity Identity { get; init; } = identity;
    public RequestPipeline Pipe { get; set; } = new RequestPipelineBuilder().
        WithConfigProvider(configurationsProvider)
        .WithRepoProvider(store.CreateStore().GetRepoFactory())
        .WithBackgroundQueue(localTaskQueue)
        .WithRouter(router)
        .Build();

    public TaskQueueHandler TaskQueueHandler { get; set; } = new TaskQueueHandler(store, localTaskQueue);
    
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await TaskQueueHandler.ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }
    
    public async Task RebuildRouterAsync()
    {
        var deployments = Store.CreateStore().Context.Set<Deployment>().Include(d => d.Target)
            .Include(d => d.Endpoints).ThenInclude(e => e.Parent).ThenInclude(e => e.Pipe)
            .ThenInclude(p => p.PipeServices)
            .Include(d => d.Endpoints).ThenInclude(e => e.Parent).ThenInclude(e => e.Pipe)
            .ThenInclude(p => p.PluginConfigs);
        await deployments.LoadAsync();
        Pipe.Router = RouterFactory.BuildRouteTrie(deployments.ToList());
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
    private PluginManager PluginManager { get; set; } = new PluginManager(configuration);
    
    public async Task<GatewayBuild> Build()
    {
        var identity = new Identity.Identity(_configuration);
        var router = WithRouter();
        var gateway = new Gateway(_configuration, StoreFactory, LocalTaskQueue, ConfigurationsProvider, PluginManager, identity, router);
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

    private RouteTrie WithRouter()
    {
        if (StoreFactory == null)
        {
            throw new InvalidOperationException("StoreFactory must be set before building the router.");
        }

        var deployments = StoreFactory.CreateStore().Context.Set<Deployment>().Include(d => d.Target)
            .Include(d => d.Endpoints).ThenInclude(e => e.Parent).ThenInclude(e => e.Pipe)
            .ThenInclude(p => p.PipeServices)
            .Include(d => d.Endpoints).ThenInclude(e => e.Parent).ThenInclude(e => e.Pipe)
            .ThenInclude(p => p.PluginConfigs);
        deployments.Load();
        return RouterFactory.BuildRouteTrie(deployments.ToList());
        
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
        
        return await Build();
    }
}