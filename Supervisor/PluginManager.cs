using GatewayPluginContract;

namespace Supervisor;


public class PluginManager(IConfiguration configuration) 
{
    private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private List<IPlugin> _plugins = [];
    internal readonly PluginServiceRegistrar Registrar = new();
    
    public ServiceTypes GetServiceTypeByIdentifier(string identifier)
    {
        var service = Registrar.GetServiceByName<IService>(identifier);
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
    
    public void LoadInternalServices(string serviceNamespace = "Gateway.services")
    {
        // Use reflection to find all classes that implement IService in the specified namespace
        var serviceType = typeof(IService);
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes();
            foreach (var type in types)
            {
                if (!type.IsClass || type.IsAbstract || !serviceType.IsAssignableFrom(type) ||
                    type.Namespace != serviceNamespace) continue;
                // TODO: Only give them their own configuration, e.g _configuration.GetSection("coreServices").
                // Allows for multiple instances of the same service with different configurations
                // Also only load services that are registered in the configuration
                foreach (var serviceConfig in _configuration.GetSection("coreServices").GetChildren())
                {
                    if (serviceConfig["identifier"] != "Core/"+type.Name)
                    {
                        continue;
                    }
                    
                    var instance = Activator.CreateInstance(type, serviceConfig
                                       .GetSection("Configuration"))
                                   ?? throw new InvalidOperationException($"Failed to create instance of service {type.Name}.");
                    Registrar.RegisterInternalServiceWithRuntimeType(type, instance, serviceConfig["Identifier"] 
                        ?? throw new InvalidOperationException("Service name not found in configuration."));
                    Console.WriteLine($"Registered internal service: {type.Name} with identifier {serviceConfig["Identifier"]}");
                    
                }
            }
        }
    }

    public Task LoadPluginsAsync(string path)
    {
        Registrar.Reset();
        _plugins.Clear();
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
                
                plugin.ConfigurePluginRegistrar(Registrar);
            }
        }
        ResolveDependencies();
        return Task.CompletedTask;
    }
    
public class PluginServiceRegistrar : IPluginServiceRegistrar
{
    private readonly Dictionary<string, ServiceContainer<IService>> _services = new();
    public IEnumerable<T>  GetServicesByType<T>(ServiceTypes serviceType) where T : IService
    {
        return _services.Values
            .Where(s => s.ServiceType == serviceType && s.Instance is T)
            .Select(s => (T)s.Instance);
    }
    
    public ServiceContainer<T> GetServiceByName<T>(string name) where T : IService
    {
        
        if (_services.TryGetValue(name, out var serviceContainer) && serviceContainer.Instance is T service)
            return new ServiceContainer<T>
            {
                Instance = service,
                ServiceType = serviceContainer.ServiceType,
                Identity = serviceContainer.Identity
            };
        throw new KeyNotFoundException($"Service '{name}' not found.");
    }

    public void RegisterService<T>(IPlugin? parentPlugin, T service, ServiceTypes serviceType) where T : IService
    {
        PluginManifest manifest;
        if (parentPlugin == null)
        {
            manifest = new PluginManifest
            {
                Name = "Core",
                Version = 0.0,
                Description = "Core services provided internally.",
                Author = "Gateway",
                Dependencies = []
            };
        }
        else
        {
            manifest = parentPlugin.GetManifest();
        }
        var identifier = manifest.Name + manifest.Version + "/" + typeof(T).Name ?? "";
        
        
        _services[identifier] = new ServiceContainer<IService>
        {
            Instance = service,
            ServiceType = serviceType,
            Identity = new ServiceIdentity
            {
                Identifier = identifier,
                OriginManifest = manifest,
            }
            
        };
        
        Console.WriteLine($"Registered service: {identifier} of type {serviceType}");
    }
    
    internal void RegisterInternalService(IService service, string serviceName)
    {
        PluginManifest manifest = new PluginManifest
        {
            Name = "Core",
            Version = 0.0,
            Description = "Core services provided internally.",
            Author = "Gateway",
            Dependencies = []
        };

        _services[serviceName] = new ServiceContainer<IService>
        {
            Instance = service,
            ServiceType = ServiceTypes.Core,
            Identity = new ServiceIdentity
            {
                Identifier = serviceName,
                OriginManifest = manifest,
            }
        };

        Console.WriteLine($"Registered internal service: {serviceName} of type {ServiceTypes.Core}");
    }    
    public void RegisterInternalServiceWithRuntimeType(Type serviceInstanceType, object serviceInstance, string componentName)
    {
        // var method = typeof(PluginServiceRegistrar).GetMethod(nameof(RegisterInternalService));
        // var genericMethod = method?.MakeGenericMethod(serviceInstanceType);
        // genericMethod?.Invoke(this, new[] { serviceInstance, serviceInstanceType, componentName });
        RegisterInternalService((IService)serviceInstance, componentName);
    }
    
    
    public void Reset()
    {
        _services.Clear();
    }
}
}