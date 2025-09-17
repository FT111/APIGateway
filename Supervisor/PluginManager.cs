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
    
    internal void LoadInternalServices(string serviceNamespace = "Gateway.services")
    {
        // Use reflection to find all classes that implement IService in the specified namespace
        var serviceType = typeof(IService);
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in assemblies)
        {
            var types = assembly.GetTypes();
            // Add unregistered core services from the registrar
            types = types.Concat(Registrar.UnregisteredCoreServices).ToArray();
            foreach (var type in types)
            {
                if (!type.IsClass || type.IsAbstract || !serviceType.IsAssignableFrom(type) ||
                    type.Namespace != serviceNamespace) continue;
                foreach (var serviceConfig in _configuration.GetSection("coreServices").GetChildren())
                {
                    if (serviceConfig["identifier"] != "Core/"+type.Name)
                    {
                        continue;
                    }

                    var identifier = serviceConfig["Identifier"];
                    if (serviceConfig.GetValue<string>("SubIdentifier") is not null)
                    {
                        identifier += "/" + serviceConfig.GetValue<string>("SubIdentifier");
                    }
                    
                    var instance = Activator.CreateInstance(type, serviceConfig
                                       .GetSection("Configuration"))
                                   ?? throw new InvalidOperationException($"Failed to create instance of service {type.Name}.");
                    Registrar.RegisterInternalServiceWithRuntimeType(type, instance, identifier
                        ?? throw new InvalidOperationException("Service name not found in configuration."));
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
    public readonly List<Type> UnregisteredCoreServices = [];
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

    public void RegisterService<T>(IPlugin parentPlugin, T service, ServiceTypes serviceType) where T : IService
    {
        // Core Services are only instantiated if used in the configuration
        if (serviceType == ServiceTypes.Core)
        {
            UnregisteredCoreServices.Add(typeof(T));
            return; 
        }
        
        if (_services.ContainsKey(typeof(T).Name))
        {
            throw new InvalidOperationException($"Service '{typeof(T).Name}' is already registered.");
        }
        
        var manifest = parentPlugin.GetManifest();
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
        if (_services.ContainsKey(serviceName))
        {
            throw new InvalidOperationException($"Internal Service '{serviceName}' is already registered." +
                                                $"Use SubIdentifiers to register multiple instances of the same core service type.");
        }
        PluginManifest manifest = new PluginManifest
        {
            Name = "Internal",
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
    internal void RegisterInternalServiceWithRuntimeType(Type serviceInstanceType, object serviceInstance, string componentName)
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