using GatewayPluginContract;
using Endpoint = GatewayPluginContract.Entities.Endpoint;
using GatewayPluginContract.Entities;

namespace Gateway.services;
using System.Collections.Generic;

public class ConfigProvider : IConfigurationsProvider
{
    private readonly IConfiguration _configuration;
    
    // Cached configurations
    private PipeConfiguration _globalConfiguration = new PipeConfiguration
    {
        PreProcessors = [],
        PostProcessors = [],
        Forwarder = new HttpRequestForwarder()
    };
    private readonly Dictionary<string, PipeConfiguration> _endpointConfigurations = new Dictionary<string, PipeConfiguration>();
    
    // Service configurations cache
    private Dictionary<string, Dictionary<string, string>> _globalServiceConfigs = new Dictionary<string, Dictionary<string, string>>();
    private Dictionary<string, Dictionary<string, Dictionary<string, string>>> _endpointServiceConfigs = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
    
    // Global pipe services cache
    private ICollection<PipeService> _globalPipeServices = new List<PipeService>();
    private Dictionary<string, ICollection<PipeService>> _endpointPipeServices = new Dictionary<string, ICollection<PipeService>>();
    
    private IRepoFactory? _dataRepos;
    private PluginManager? _pluginManager;

    public ConfigProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private void _AddProcessorFromRegistryIfAvailable(string serviceName, uint order, ServiceFailurePolicies onFailure, List<PipeProcessorContainer> processorList,
        PluginManager.PluginServiceRegistrar registry)
    {
        try
        {
            var service = registry.GetServiceByName<GatewayPluginContract.IRequestProcessor>(serviceName);
            if (service?.Instance is not GatewayPluginContract.IRequestProcessor requestProcessor) return;

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
        _dataRepos = repoFactory;
        _pluginManager = pluginManager;
        
        // Load initial configurations
        LoadPipeConfigs();
        LoadServiceConfigs();

        Console.WriteLine("ConfigProvider initialised.");
    }

    public Task<PipeConfiguration> GetPipeConfigAsync(Endpoint? endpoint = null)
    {
        if (_pluginManager == null)
        {
            return Task.FromResult(_globalConfiguration);
        }

        ICollection<PipeService>? services;
        
        try
        {
            if (endpoint == null)
            {
                // Use cached global pipe services
                services = _globalPipeServices;
            }
            else
            {
                // Use cached endpoint-specific pipe services
                if (!_endpointPipeServices.TryGetValue(endpoint.Path, out services))
                {
                    services = endpoint.Pipe?.Pipeservices;
                }
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

    public Task<Dictionary<string, Dictionary<string, string>>> GetServiceConfigsAsync(Endpoint? endpoint = null)
    {
        if (_dataRepos == null)
        {
            throw new InvalidOperationException("Repository factory is not initialized.");
        }

        if (endpoint == null)
        {
            // Return cached global service configs
            return Task.FromResult(_globalServiceConfigs);
        }
        else
        {
            // Return cached endpoint-specific service configs
            if (_endpointServiceConfigs.TryGetValue(endpoint.Path, out var configs))
            {
                return Task.FromResult(configs);
            }
            
            // If no configs found for this endpoint, return global configs
            Console.WriteLine("No configs found for the specified endpoint. Using global configs.");
            return Task.FromResult(_globalServiceConfigs);
        }
    }

    public void LoadPipeConfigs()
    {
        if (_dataRepos == null) return;
        
        try
        {
            // Load global pipe services
            var globalPipe = _dataRepos.GetRepo<Pipe>().QueryAsync(pipe => pipe.Global == true).Result.FirstOrDefault();
            if (globalPipe != null)
            {
                _globalPipeServices = globalPipe.Pipeservices ?? new List<PipeService>();
            }
            
            // Load endpoint-specific pipe services
            var endpoints = _dataRepos.GetRepo<Endpoint>().QueryAsync(_ => true).Result;
            foreach (var endpoint in endpoints)
            {
                if (endpoint.Pipe != null)
                {
                    _endpointPipeServices[endpoint.Path] = endpoint.Pipe.Pipeservices ?? new List<PipeService>();
                }
            }
            
            Console.WriteLine($"Loaded pipe configurations for {_endpointPipeServices.Count} endpoints");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load pipe configurations: {e.Message}");
        }
    }
    
    public void LoadServiceConfigs()
    {
        if (_dataRepos == null) return;
        
        try
        {
            // Load global service configs
            var globalPipe = _dataRepos.GetRepo<Pipe>().QueryAsync(pipe => pipe.Global == true).Result.FirstOrDefault();
            if (globalPipe != null)
            {
                _globalServiceConfigs = ConvertPluginConfigsToDict(globalPipe.PluginConfigs);
            }
            
            // Load endpoint-specific service configs
            var endpoints = _dataRepos.GetRepo<Endpoint>().QueryAsync(_ => true).Result;
            foreach (var endpoint in endpoints)
            {
                if (endpoint.Pipe?.PluginConfigs != null)
                {
                    _endpointServiceConfigs[endpoint.Path] = ConvertPluginConfigsToDict(endpoint.Pipe.PluginConfigs);
                }
            }
            
            Console.WriteLine($"Loaded service configurations for {_endpointServiceConfigs.Count} endpoints");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to load service configurations: {e.Message}");
        }
    }
    
    private Dictionary<string, Dictionary<string, string>> ConvertPluginConfigsToDict(ICollection<PluginConfig>? configs)
    {
        var result = new Dictionary<string, Dictionary<string, string>>();
        if (configs == null) return result;
        
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
}