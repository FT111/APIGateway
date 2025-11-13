using GatewayPluginContract;
using Endpoint = GatewayPluginContract.Entities.Endpoint;
using GatewayPluginContract.Entities;

namespace Gateway.services;
using System.Collections.Generic;

public class ConfigProvider : IConfigurationsProvider
{
    private readonly IConfiguration _configuration;
    
    public ConfigProvider(IConfiguration configuration, PluginManager pluginManager)
    {
        _configuration = configuration;
        _pluginManager = pluginManager;
    }
    
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
    
    private Repositories? _dataRepos;
    private PluginManager? _pluginManager;

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
            
        }
    }

    public PipeConfiguration GetPipeFromDefinition(ICollection<PipeService> services)
    {
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
                var type = _pluginManager?.GetServiceTypeByIdentifier(identifier);
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
                
            }
        }

        return pipe;
    }
    
    

    public Dictionary<string, Dictionary<string, string>> ConvertPluginConfigsToDict(ICollection<PluginConfig>? configs)
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