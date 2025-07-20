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

    private void _AddProcessorFromRegistryIfAvailable(string serviceName, uint order, ServiceFailurePolicies onFailure, List<PipeProcessorContainer> processorList,
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
                FailurePolicy = ServiceFailurePolicies.Ignore,
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

        _store = store;
        _pluginManager = pluginManager;

        
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
    
    public Task<PipeConfiguration> GetPipeConfigAsync(string? endpoint = null)
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
        
        foreach (var serviceRecipeContainer in pipeRecipe.ServiceList)
        {
            try
            {
                var type = _pluginManager.GetServiceTypeByIdentifier(serviceRecipeContainer.Identifier);
                switch (type)
                {
                    case ServiceTypes.PreProcessor:
                        _AddProcessorFromRegistryIfAvailable(serviceRecipeContainer.Identifier, 0, serviceRecipeContainer.FailurePolicy, pipe.PreProcessors, _pluginManager.Registrar);
                        break;
                    case ServiceTypes.PostProcessor:
                        _AddProcessorFromRegistryIfAvailable(serviceRecipeContainer.Identifier, 0, serviceRecipeContainer.FailurePolicy, pipe.PostProcessors, _pluginManager.Registrar);
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to load service '{serviceRecipeContainer}': {e.Message}");
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
        return _store.GetPluginConfigsAsync(endpoint);
    }
    public Task SetServiceConfigAsync(string scope, string key, string value, string? endpoint = null)
    {
        // For testing, do nothing
        return Task.CompletedTask;
    }
}