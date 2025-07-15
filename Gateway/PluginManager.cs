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
        return Task.CompletedTask;
    }

public class PluginServiceRegistrar : IPluginServiceRegistrar
{
    private readonly Dictionary<string, ServiceContainer> _services = new();
    // private readonly Dictionary<string, Dictionary<string, string>> _configurations = new();
    
    // public void RegisterConfiguration(string pluginName, string key, string value)
    // {
    //     if (!_configurations.ContainsKey(pluginName))
    //     {
    //         _configurations[pluginName] = new Dictionary<string, string>();
    //     }
    //
    //     _configurations[pluginName][key] = value;
    // }
    //
    // public string? GetConfiguration(string pluginName, string key)
    // {
    //     if (_configurations.TryGetValue(pluginName, out var config) && config.TryGetValue(key, out var value))
    //     {
    //         return value;
    //     }
    //     return null;
    // }
    //
    // public void UpdateConfigurationKey(IPlugin plugin, string key, string value)
    // {
    //     var pluginName = plugin.GetManifest().Name;
    //     if (_configurations.ContainsKey(pluginName))
    //     {
    //         _configurations[pluginName][key] = value;
    //         plugin.UpdateConfigKey(key, value);
    //     } else
    //     {
    //         throw new KeyNotFoundException($"Configuration for plugin '{pluginName}' not found.");
    //     }
    // }
    
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