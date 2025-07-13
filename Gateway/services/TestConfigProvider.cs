using GatewayPluginContract;

namespace Gateway.services;
using System.Collections.Generic;


public class TestConfigProvider : IConfigurationsProvider
{
    private PipeConfiguration _globalConfiguration = new PipeConfiguration
    {
        PreProcessors = [],
        PostProcessors = [],
        Forwarder = new HttpRequestForwarder()
    };
    
    private IStore? _store;
    private PluginManager? _pluginManager;
    private readonly Dictionary<string, PipeConfiguration> _endpointConfigurations = new Dictionary<string, PipeConfiguration>();

    private void _AddProcessorFromRegistryIfAvailable(string serviceName, uint order, List<PipeProcessorContainer> processorList,
        PluginManager.PluginServiceRegistrar registry)
    {
        try
        {
            // Attempt to get the service by name from the registry
            var service = registry.GetServiceByName(serviceName);
            // Check if the service is null or not loaded - if so, skip it. (In the future, plugin dependencies can be used to ensure the service is loaded)
            if (service?.Instance is not GatewayPluginContract.IRequestProcessor requestProcessor) return;
            
            // Add the processor to the list with the specified order
            processorList.Add(new PipeProcessorContainer
            {
                Processor = requestProcessor,
                Order = order,
                Identifier = serviceName
            });
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load {serviceName}: {e.Message}");
        }
    }
    
    public async Task InitialiseAsync(PluginManager pluginManager, IStore store)
    {

        var services = store.GetPipeConfigRecipeAsync();
        _store = store;
        _pluginManager = pluginManager;
        _AddProcessorFromRegistryIfAvailable(
            "TestPlugin1.0.0/TestPreProcessor",
            0,
            _globalConfiguration.PreProcessors,
            pluginManager.Registrar
            );
        _AddProcessorFromRegistryIfAvailable(
            "TestPlugin1.0.0/TestPostProcessor",
            0,
            _globalConfiguration.PostProcessors,
            pluginManager.Registrar
            );
        
        Console.WriteLine("TestConfigProvider initialised.");
        
        await Task.Delay(100); // Simulate async operation
    }
    
    public Task<Dictionary<string, PipeConfiguration>> GetAllPipeConfigsAsync()
    {
        // For testing, return a dictionary with a single entry
        return Task.FromResult(new Dictionary<string, PipeConfiguration>
        {
            { "global", _globalConfiguration }
        });
    }
    
    public Task<PipeConfiguration> GetPipeConfigsAsync(string? endpoint = null)
    {
        if (_store == null || _pluginManager == null)
        {
            return Task.FromResult(_globalConfiguration);
        }

        PipeConfigurationRecipe pipeRecipe;
        try
        {
            pipeRecipe = _store.GetPipeConfigRecipeAsync(endpoint).Result;
        }
        catch (Exception e)
        {
            return Task.FromResult(_globalConfiguration);
        }
        
        var pipe = new PipeConfiguration
        {
            PreProcessors = new List<PipeProcessorContainer>(),
            PostProcessors = new List<PipeProcessorContainer>(),
            Forwarder = new HttpRequestForwarder()
        };
        
        foreach (var serviceIdentifier in pipeRecipe.ServiceList)
        {
            var type = _pluginManager.GetServiceTypeByIdentifier(serviceIdentifier);
            switch (type)
            {
                case ServiceTypes.PreProcessor:
                    _AddProcessorFromRegistryIfAvailable(serviceIdentifier, 0, pipe.PreProcessors, _pluginManager.Registrar);
                    break;
                case ServiceTypes.PostProcessor:
                    _AddProcessorFromRegistryIfAvailable(serviceIdentifier, 0, pipe.PostProcessors, _pluginManager.Registrar);
                    break;
            }
        }
        
        Console.WriteLine($"Loaded pipe configuration for endpoint '{endpoint ?? "global"}' with {pipeRecipe.ServiceList.Count} services ");
        return Task.FromResult(pipe);
    }
    
    public Task SetPipeConfigAsync(PipeConfiguration configuration, string? endpoint = null)
    {
        if (endpoint == null)
        {
            _globalConfiguration = configuration;
        }
        else
        {
            _endpointConfigurations[endpoint] = configuration;
        }
        
        return Task.CompletedTask;
    }
    
    public Task<Dictionary<string, Dictionary<string, string>>> GetAllServiceConfigsAsync()
    {
        // For testing, return an empty dictionary
        return Task.FromResult(new Dictionary<string, Dictionary<string, string>>());
    }
    public Task<Dictionary<string, Dictionary<string, string>>> GetServiceConfigsAsync(string? endpoint = null)
    {
        return _store.GetAsync<Dictionary<string, Dictionary<string, string>>>(endpoint);
    }
    public Task SetServiceConfigAsync(string scope, string key, string value, string? endpoint = null)
    {
        // For testing, do nothing
        return Task.CompletedTask;
    }
}