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
    private readonly PluginServiceRegistrar _registrar = new();

    public Task LoadPluginsAsync(string path)
    {
        var pluginLoaders = PluginLoader.GetPluginLoaders(path);
        
        foreach (var loader in pluginLoaders)
        {
            var plugin = loader.LoadDefaultAssembly().CreateInstance("IPlugin") as IPlugin;
            if (plugin == null)
            {
                continue;
            }

            _plugins.Add(plugin);
            
            plugin.ConfigureRegistrar(_registrar);
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
    


    public void RegisterService<T>(T service, ServiceTypes serviceType) where T : IService
    {
        _services[typeof(T).FullName ?? ""] = new ServiceContainer
        {
            Instance = service,
            ServiceType = serviceType
            
        };
    }
}
}