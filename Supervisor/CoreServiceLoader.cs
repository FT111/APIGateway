using GatewayPluginContract;
using Supervisor.auth;

namespace Supervisor;

public static class CoreServiceLoader
{
    public static void LoadFromConfiguration(WebApplicationBuilder builder)
    {
        var pluginManager = new PluginManager(builder.Configuration);
        pluginManager.LoadPluginsAsync("services/plugins");
        pluginManager.LoadInternalServices("Supervisor.services");
        
        var coreServices = builder.Configuration.GetSection("coreServices") 
            ?? throw new InvalidOperationException("coreServices configuration section not found.");
        
        var requiredServices = new[] {"InstanceStore", "MessageAdapter", "SupervisorStore"};
        Dictionary<string, string> serviceIdentifiers = new Dictionary<string, string>();
        foreach (var service in requiredServices)
        {
            var identifier = coreServices.GetSection(service)["Identifier"];
            if (coreServices.GetSection(service)["SubIdentifier"] is not null)
            {
                identifier += "/" + coreServices.GetSection(service)["SubIdentifier"];
            }
            serviceIdentifiers.Add(service, identifier
                                            ?? throw new InvalidOperationException($"Service '{service}' not found in configuration."));
        }

        var storeFactory = pluginManager.Registrar.GetServiceByName<StoreFactory>(serviceIdentifiers["InstanceStore"]).Instance
            ?? throw new InvalidOperationException("StoreFactory service not found.");
        
        builder.Services.AddSingleton<StoreFactory>(storeFactory);
        builder.Services.AddSingleton<IGatewayRepositories>(storeFactory.CreateStore().GetRepoFactory());
        builder.Services.AddSingleton<AuthHandler>(new AuthHandler(builder.Configuration));
        
        builder.Services.AddSingleton<SupervisorAdapter>(pluginManager.Registrar.GetServiceByName<SupervisorAdapter>(serviceIdentifiers["MessageAdapter"]).Instance
            ?? throw new InvalidOperationException("SupervisorClient service not found."));
    }
}