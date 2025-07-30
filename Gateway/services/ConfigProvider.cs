using GatewayPluginContract;
using Endpoint = GatewayPluginContract.Entities.Endpoint;
using GatewayPluginContract.Entities;

namespace Gateway.services;
using System.Collections.Generic;


public class ConfigProvider : IConfigurationsProvider
{
    private PipeConfiguration _globalConfiguration = new PipeConfiguration
    {
        PreProcessors = [],
        PostProcessors = [],
        Forwarder = new HttpRequestForwarder()
    };
    
    private IRepoFactory? _DataRepos;
    private PluginManager? _pluginManager;
    private readonly Dictionary<string, PipeConfiguration> _endpointConfigurations = new Dictionary<string, PipeConfiguration>();

    private void _AddProcessorFromRegistryIfAvailable(string serviceName, uint order, ServiceFailurePolicies onFailure, List<PipeProcessorContainer> processorList,
        PluginManager.PluginServiceRegistrar registry)
    {
        try
        {
            // Attempt to get the service by name from the registry
            var service = registry.GetServiceByName<GatewayPluginContract.IRequestProcessor>(serviceName);
            // Check if the service is null or not loaded - if so, skip it. (In the future, plugin dependencies can be used to ensure the service is loaded)
            if (service?.Instance is not GatewayPluginContract.IRequestProcessor requestProcessor) return;
            
            // Add the processor to the list with the specified order
            processorList.Add(new PipeProcessorContainer
            {
                Processor = service,
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
    
    public async Task InitialiseAsync(PluginManager pluginManager, IRepoFactory repoFactory)
    {

        _DataRepos = repoFactory;
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
    
    public Task<PipeConfiguration> GetPipeConfigAsync(Endpoint? endpoint = null)
    {
        if (_DataRepos == null || _pluginManager == null)
        {
            return Task.FromResult(_globalConfiguration);
        }

        ICollection<PipeService>? services;
        try
        {
            if (endpoint == null)
            {
                // If no endpoint is specified, return the global pipe configuration
                services = _DataRepos.GetRepo<Pipe>().QueryAsync(pipe => pipe.Global == true).Result.FirstOrDefault()?.Pipeservices;
            }
            else
            {
                // If an endpoint is specified, get the pipe configuration for that endpoint
                services = endpoint.Pipe?.Pipeservices;
            }
            
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
        
        foreach (var service in services ?? [])
        {
            try
            {
                var identifier = service.PluginTitle + service.PluginVersion + "/" + service.ServiceTitle;
                var type = _pluginManager.GetServiceTypeByIdentifier(identifier);
                switch (type)
                {
                    case ServiceTypes.PreProcessor:
                        _AddProcessorFromRegistryIfAvailable(identifier, 0, service.FailurePolicy, pipe.PreProcessors, _pluginManager.Registrar);
                        break;
                    case ServiceTypes.PostProcessor:
                        _AddProcessorFromRegistryIfAvailable(identifier, 0, service.FailurePolicy, pipe.PostProcessors, _pluginManager.Registrar);
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to load service '{service}': {e.Message}");
            }
        }
        
        Console.WriteLine($"Loaded pipe configuration for endpoint '{endpoint?.Path ?? "global"}' with {
            pipe.PreProcessors.Count + pipe.PostProcessors.Count
        } services ");
        return Task.FromResult(pipe);
    }
    
    public Task SetPipeConfigAsync(PipeConfiguration configuration, Endpoint? endpoint = null)
    {
        if (endpoint == null)
        {
            _globalConfiguration = configuration;
        }
        else
        {
            _endpointConfigurations[endpoint.Path] = configuration;
        }
        
        return Task.CompletedTask;
    }
    
    public Task<Dictionary<string, Dictionary<string, string>>> GetAllServiceConfigsAsync()
    {
        // For testing, return an empty dictionary
        return Task.FromResult(new Dictionary<string, Dictionary<string, string>>());
    }
    public async Task<Dictionary<string, Dictionary<string, string>>> GetServiceConfigsAsync(Endpoint? endpoint = null)
    {
        if (_DataRepos == null)
        {
            throw new InvalidOperationException("Repository factory is not initialized.");
        }

        // Retrieve the configs, either from the specified endpoint or from the global pipe
        ICollection<PluginConfig>? configs;
        if (endpoint == null)
        {
            configs = _DataRepos.GetRepo<Pipe>().QueryAsync(pipe => pipe.Global == true ).Result.FirstOrDefault()?.PluginConfigs;
        }
        else
        {
            configs = endpoint.Pipe?.PluginConfigs;
        }
        
        if (configs == null)
        {
            Console.WriteLine("No configs found for the specified endpoint. Using global configs.");
            configs = _DataRepos.GetRepo<Pipe>().QueryAsync(pipe => pipe.Global == true).Result.First()?.PluginConfigs;
            
        }
        // Convert the collection of PluginConfig to an indexed dictionary
        var result = new Dictionary<string, Dictionary<string, string>>();
        foreach (var config in configs)
        {
            if (!result.ContainsKey(config.Namespace))
            {
                result[config.Namespace] = new Dictionary<string, string>();
            }
            result[config.Namespace][config.Key] = config.Value;
        }
        return result;
    }
    public Task SetServiceConfigAsync(string scope, string key, string value, Endpoint? endpoint = null)
    {
        // For testing, do nothing
        return Task.CompletedTask;
    }
}
