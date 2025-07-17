using GatewayPluginContract;

namespace Gateway;


public class ServiceContainer
{
    public required IService  Instance { get; set; }
    public required ServiceTypes ServiceType { get; set; }
    // public required Dictionary<string, string> PluginConfiguration { get; set; }
}


public class PluginManager
{
    private List<IPlugin> _plugins = [];
    internal readonly PluginServiceRegistrar Registrar = new();
    
    public ServiceTypes GetServiceTypeByIdentifier(string identifier)
    {
        var service = Registrar.GetServiceByName(identifier);
        return service?.ServiceType ?? throw new KeyNotFoundException($"Service '{identifier}' not found.");
    }


    private void ResolveDependencies()
    {
        foreach (var plugin in _plugins)
        {
            var manifest = plugin.GetManifest();
            if (manifest.Dependencies.Count == 0) continue;
            
            manifest.Dependencies?.ForEach(dep =>
            {
                // Check if the dependency is provided
                if (_plugins.Any(p =>
                        p.GetManifest().Name == dep.Name && dep.VersionCheck(p.GetManifest().Version)))
                {
                    // If the dependency is provided, set it as provided
                    Console.WriteLine($"Dependency '{dep.Name}' found for plugin '{manifest.Name}'.");
                    dep.IsProvided = true;
                    return;
                }
                    
                // If the dependency is optional, log a message and continue
                if (dep.IsOptional)
                {
                    Console.WriteLine($"Optional dependency '{dep.Name}' not found for plugin '{manifest.Name}'. Continuing.");
                    dep.IsProvided = false;
                }
                else
                {
                    // If the dependency is required, throw an exception
                    throw new Exception($"Required dependency '{dep.Name}' not found for plugin '{manifest.Name}'.");
                }
            });
        }
    }

    public Task LoadPluginsAsync(string path)
    {
        List<McMaster.NETCore.Plugins.PluginLoader> pluginLoaders = PluginLoader.GetPluginLoaders(path);

        foreach (var pluginLoader in pluginLoaders)
        {
            foreach (var pluginLoaderType in pluginLoader.LoadDefaultAssembly().GetTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t) && t is { IsAbstract: false, IsClass: true }))
            {
                var plugin = pluginLoader.LoadDefaultAssembly().CreateInstance(pluginLoaderType.FullName) as IPlugin;
                if (plugin == null)
                {
                    continue;
                }

                _plugins.Add(plugin);
                
                plugin.ConfigureRegistrar(Registrar);
            }
        }
        ResolveDependencies();
        return Task.CompletedTask;
    }

public class PluginServiceRegistrar : IPluginServiceRegistrar
{
    private readonly Dictionary<string, ServiceContainer> _services = new();
    
    public IEnumerable<T>  GetServicesByType<T>(ServiceTypes serviceType) where T : IService
    {
        return _services.Values
            .Where(s => s.ServiceType == serviceType && s.Instance is T)
            .Select(s => (T)s.Instance);
    }
    
    public ServiceContainer? GetServiceByName(string name)
    {
        return _services[name];
        return _services.TryGetValue(name, out var serviceContainer) ? serviceContainer : throw new KeyNotFoundException($"Service '{name}' not found.");
    }
    


    public void RegisterService<T>(IPlugin parentPlugin, T service, ServiceTypes serviceType) where T : IService
    {
        var title = parentPlugin.GetManifest().Name + parentPlugin.GetManifest().Version + "/" + typeof(T).Name ?? "";
        _services[title] = new ServiceContainer
        {
            Instance = service,
            ServiceType = serviceType
        };
        
        Console.WriteLine($"Registered service: {title} of type {serviceType}");
        
    }
}
}