using System.IO.Compression;
using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gateway;


public class PluginManager
{
    private readonly IConfiguration _configuration;
    internal List<IPlugin> Plugins = [];
    internal string PluginDeliveryUrl = string.Empty;
    internal readonly PluginServiceRegistrar Registrar = new();
    private List<Func<IPlugin, Task>> _pluginLoadPipeline = [];

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
        
        _configuration =  configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    internal void AddPluginLoadStep(Func<IPlugin, Task> step)
    {
        _pluginLoadPipeline.Add(step);
    }
    
    public class PluginVerificationResult
    {
        public List<string> Missing { get; set; } = [];
        public List<string> Removed { get; set; } = [];
        public bool IsValid => Missing.Count == 0 && Removed.Count == 0;
    }
    
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
                var instance = Activator.CreateInstance(type, _configuration);
                Registrar.RegisterServiceWithTypeDef(type, null, instance, ServiceTypes.Core);
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
                // Create an instance of the plugin from the assembly found
                var plugin = pluginLoader.LoadDefaultAssembly().CreateInstance(pluginLoaderType.FullName) as IPlugin;
                if (plugin == null)
                {
                    continue;
                }
                
                // Run the plugin load pipeline
                foreach (var step in _pluginLoadPipeline)
                {
                    step(plugin);
                }
            }
        }
        ResolveDependencies();
        return Task.CompletedTask;
    }

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
        Console.WriteLine($"Downloading plugin from: {fullDeliveryUrl}");
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