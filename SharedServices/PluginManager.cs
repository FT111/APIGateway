using System.IO.Compression;
using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace SharedServices;


public class PluginManager : IPluginManager
{
    private readonly IConfiguration _configuration;
    internal readonly List<IPlugin> LoadedPlugins = [];
    internal string PluginDeliveryUrl = string.Empty;
    internal readonly PluginServiceRegistrar ServiceRegistrar = new();
    private List<Func<IPlugin, Task>> _pluginLoadPipeline = [];
    
    public List<IPlugin> GetPlugins => LoadedPlugins;
    public List<IPlugin> Plugins => LoadedPlugins;
    public IPluginServiceRegistrar Registrar => ServiceRegistrar;
    
    public PluginManager(IConfiguration configuration)
    {
        // Setup default load pipeline
        AddPluginLoadStep(plugin =>
        {
            Plugins.Add(plugin);
            return Task.CompletedTask;
        });
        AddPluginLoadStep(plugin =>
        {
            plugin.ConfigurePluginRegistrar(Registrar);
            return Task.CompletedTask;
        });
        
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }
    
    public void AddPluginLoadStep(Func<IPlugin, Task> step)
    {
        _pluginLoadPipeline.Add(step);
    }
    
    // public class PluginVerificationResult
    // {
    //     public List<string> Missing { get; set; } = [];
    //     public List<string> Removed { get; set; } = [];
    //     public bool IsValid => Missing.Count == 0 && Removed.Count == 0;
    // }

    public ServiceTypes GetServiceTypeByIdentifier(string identifier)
    {
        var service = Registrar.GetServiceByName<IService>(identifier);
        return service?.ServiceType ?? throw new KeyNotFoundException($"Service '{identifier}' not found.");
    }


    private void ResolveDependencies()
    {
        foreach (var plugin in Plugins)
        {
            var manifest = plugin.GetManifest();
            if (manifest.Dependencies.Count == 0) continue;
            
            manifest.Dependencies?.ForEach(dep =>
            {
                // Check if the dependency is provided
                if (Plugins.Any(p =>
                        p.GetManifest().Name == dep.Name && dep.VersionCheck(p.GetManifest().Version)))
                {
                    // If the dependency is provided, set it as provided
                    
                    dep.IsProvided = true;
                    return;
                }
                    
                // If the dependency is optional, log a message and continue
                if (dep.IsOptional)
                {
                    
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
            // Add unregistered core services from the registrar
            types = types.Concat(ServiceRegistrar.UnregisteredCoreServices).ToArray();
            foreach (var type in types)
            {
                if (!type.IsClass || type.IsAbstract || !serviceType.IsAssignableFrom(type) ||
                    type.Namespace != serviceNamespace)
                {
                    continue;
                };
                foreach (var serviceConfig in _configuration.GetSection("coreServices").GetChildren())
                {
                    if (serviceConfig["identifier"] != "Core/"+type.Name)
                    {
                        continue;
                    }

                    var identifier = serviceConfig["Identifier"];
                    if (serviceConfig["SubIdentifier"] is not null)
                    {
                        identifier += "/" + serviceConfig["SubIdentifier"];
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
        Plugins.Clear();
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

                Plugins.Add(plugin);
                
                plugin.ConfigurePluginRegistrar(Registrar);
            }
        }
        ResolveDependencies();
        return Task.CompletedTask;
    }
    
        
    // public async Task<PluginVerificationResult> VerifyInstalledPluginsAsync(IQueryable<PipeService> services)
    // {
    //     var requiredPlugins = await ResolveRequiredPluginsAsync(services);
    //     var installedPlugins = new HashSet<string>(Plugins.Select(p =>
    //         p.GetManifest().Name + "_" + p.GetManifest().Version));
    //     
    //     return new PluginVerificationResult
    //     {
    //         Missing = requiredPlugins.Except(installedPlugins).ToList(),
    //         Removed = installedPlugins.Except(requiredPlugins).ToList()
    //     };
    // }
    
    
    private static async Task<HashSet<string>> ResolveRequiredPluginsAsync(IQueryable<PipeService> services)
    {
        var requiredPlugins = new HashSet<string>();
        await services.ForEachAsync(service =>
        {
            var identifier = service.PluginTitle + "_" + service.PluginVersion;
            requiredPlugins.Add(identifier);
        });
        
        return requiredPlugins;
    }
    
    public async Task<PluginVerificationResult> VerifyInstalledPluginsAsync(IQueryable<PipeService> services)
    {
        var requiredPlugins = await ResolveRequiredPluginsAsync(services);
        var installedPlugins = new HashSet<string>(Plugins.Select(p =>
            p.GetManifest().Name + "_" + p.GetManifest().Version));
        
        return new PluginVerificationResult
        {
            Missing = requiredPlugins.Except(installedPlugins).ToList(),
            Removed = installedPlugins.Except(requiredPlugins).ToList()
        };
    }

    public async Task DownloadAndInstallPluginAsync(string identifier)
    {
        var httpClient = new HttpClient();
        identifier = identifier.Replace("/", "_");
        var fullDeliveryUrl = PluginDeliveryUrl + "/" + identifier + ".gap";
        
        var response = await httpClient.GetAsync(fullDeliveryUrl);
        response.EnsureSuccessStatusCode();
        
        var pluginData = await response.Content.ReadAsByteArrayAsync();
        var pluginPath = Path.Combine(_configuration["PluginDirectory"] ?? "service/plugins", identifier + ".gap");
        await File.WriteAllBytesAsync(pluginPath, pluginData);
        
        ZipFile.ExtractToDirectory(pluginPath, Path.Combine(_configuration["PluginDirectory"] ?? "service/plugins", identifier), true);
        
        // Delete the .gap file after extraction
        if (File.Exists(pluginPath))
        {
            File.Delete(pluginPath);
        }
    }
    
    public async Task RemovePluginAsync(string identifier)
    {
        identifier = identifier.Replace("/", "_");
        var pluginPath = Path.Combine(_configuration["PluginDirectory"] ?? "service/plugins", identifier);
        if (File.Exists(pluginPath))
        {
            File.Delete(pluginPath);
        }
        else
        {
            throw new FileNotFoundException($"Plugin file '{pluginPath}' not found.");
        }
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

        
    }    
    public void RegisterInternalServiceWithRuntimeType(Type serviceInstanceType, object serviceInstance, string componentName)
    {
        // var method = typeof(PluginServiceRegistrar).GetMethod(nameof(RegisterInternalService));
        // var genericMethod = method?.MakeGenericMethod(serviceInstanceType);
        // genericMethod?.Invoke(this, new[] { serviceInstance, serviceInstanceType, componentName });
        RegisterInternalService((IService)serviceInstance, componentName);
    }
    
    public void RegisterServiceWithTypeDef(Type serviceInstanceType , IPlugin? parentPlugin, object serviceInstance, ServiceTypes serviceType)
    {
        var method = typeof(PluginServiceRegistrar).GetMethod(nameof(PluginServiceRegistrar.RegisterService));
        var genericMethod = method?.MakeGenericMethod(serviceInstanceType);
        genericMethod?.Invoke(this, new[] { parentPlugin, serviceInstance, serviceType });

    }
    
    
    public void Reset()
    {
        _services.Clear();
    }
}
}