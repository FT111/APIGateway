namespace Gateway;
using System.Collections.Generic;

public class PipeConfiguration
{
    public List<PipeProcessorContainer> PreProcessors { get; set; } = [];
    public List<PipeProcessorContainer> PostProcessors { get; set; } = [];
    public IRequestForwarder? Forwarder { get; set; } = null;
}

public interface IConfigurationsProvider
{
    void Initialise (PluginManager.PluginServiceRegistrar registry);
    Task<PipeConfiguration> GetGlobalConfigurationAsync();
    Task<Dictionary<string, PipeConfiguration>> GetEndpointConfigurationsAsync();
    Task<PipeConfiguration> GetEndpointConfigurationAsync(string endpoint);
    Task SetGlobalConfigurationAsync(PipeConfiguration configuration);
    Task SetEndpointConfigurationAsync(string endpoint, PipeConfiguration configuration);
}

public class ServiceConfigurationManager
{
    public required IConfigurationsProvider Provider { get; set; }
    
    private Dictionary<string, PipeConfiguration> EndpointConfigurations { get; set; } = new();
    public PipeConfiguration GlobalConfiguration { get; set; } = new();
    
    public async Task LoadConfigurationsAsync()
    {
        GlobalConfiguration = await Provider.GetGlobalConfigurationAsync();
        EndpointConfigurations = await Provider.GetEndpointConfigurationsAsync();
    }
    
    public async Task<PipeConfiguration> GetEndpointConfigurationAsync(string endpoint)
    {
        if (EndpointConfigurations.TryGetValue(endpoint, out var config))
        {
            return config;
        }
        return await Provider.GetEndpointConfigurationAsync(endpoint);
    }
    
    public async Task SetEndpointConfigurationAsync(string endpoint, PipeConfiguration configuration)
    {
        EndpointConfigurations[endpoint] = configuration;
        await Provider.SetEndpointConfigurationAsync(endpoint, configuration);
    }
    
    public async Task SetGlobalConfigurationAsync(PipeConfiguration configuration)
    {
        GlobalConfiguration = configuration;
        await Provider.SetGlobalConfigurationAsync(configuration);
    }
}