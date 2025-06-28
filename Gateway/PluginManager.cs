namespace Gateway;
using McMaster.NETCore.Plugins;


public class PluginManager
{
    private List<IPlugin> _plugins = [];
    private List<IService> _services = [];

    public Task LoadPluginsAsync()
    {
        var pluginLoaders = PluginLoader.GetPluginLoaders();
        var registry = new PluginServiceRegistrar();
        
        foreach (var loader in pluginLoaders)
        {
            var plugin = loader.LoadDefaultAssembly().CreateInstance("IPlugin") as IPlugin;
            if (plugin == null)
            {
                continue;
            }

            _plugins.Add(plugin);
            
            plugin.ConfigureRegistrar(registry);
        }
        return Task.CompletedTask;
}

public class PluginServiceRegistrar
{
    private readonly IServiceCollection _services = new ServiceCollection();
    private readonly Dictionary<string, Dictionary<string, string>> _configurations = new();
    
    public void RegisterConfiguration(string pluginName, string key, string value)
    {
        if (!_configurations.ContainsKey(pluginName))
        {
            _configurations[pluginName] = new Dictionary<string, string>();
        }

        _configurations[pluginName][key] = value;
    }
    
    public string? GetConfiguration(string pluginName, string key)
    {
        if (_configurations.TryGetValue(pluginName, out var config) && config.TryGetValue(key, out var value))
        {
            return value;
        }
        return null;
    }
    
    public IEnumerable<IService> GetServices()
    {
        return _services.Select(descriptor => descriptor.ImplementationInstance)
            .OfType<IService>();
    }


    public void RegisterService<T>(T service) where T : IService
    {
        _services.AddSingleton(
            new ServiceDescriptor(typeof(T), service)
            );
    }
}
}