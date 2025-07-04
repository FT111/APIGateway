namespace Gateway;
using System.Collections.Generic;

public class PipeConfiguration
{
    public required List<PipeProcessorContainer> PreProcessors { get; set; } 
    public required List<PipeProcessorContainer> PostProcessors { get; set; }
    public required IRequestForwarder? Forwarder { get; set; }
}


public interface IConfigurationsProvider
{
    Task InitialiseAsync (PluginManager.PluginServiceRegistrar registry);
    Task<PipeConfiguration> GetGlobalConfigurationAsync();
    Task<Dictionary<string, PipeConfiguration>> GetEndpointConfigurationsAsync();
    Task<PipeConfiguration> GetEndpointConfigurationAsync(string endpoint);
    Task SetGlobalConfigurationAsync(PipeConfiguration configuration);
    Task SetEndpointConfigurationAsync(string endpoint, PipeConfiguration configuration);
    Task<Dictionary<string, Dictionary<string, string>>> GetPluginConfigurationsAsync();
    Task<Dictionary<string, string>> GetEndpointPluginConfigurationsAsync(string endpoint);
    Task SetPluginConfigurationAsync(string pluginName, string key, string value);
}

public class ServiceConfigurationManager
{
    public required IConfigurationsProvider Provider { get; set; }
    
    private Dictionary<string, PipeConfiguration> EndpointConfigurations { get; set; } = new();
    public PipeConfiguration? GlobalConfiguration { get; set; } = null;
    public Dictionary<string, Dictionary<string, string>> PluginConfigurations { get; set; } = new();
    
    public async Task LoadConfigurationsAsync(PluginManager.PluginServiceRegistrar registry)
    {
        await Provider.InitialiseAsync(registry);
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
    
    public async Task SetPluginConfigurationAsync(string pluginName, string key, string value)
    {
        if (!PluginConfigurations.ContainsKey(pluginName))
        {
            PluginConfigurations[pluginName] = new Dictionary<string, string>();
        }
        
        PluginConfigurations[pluginName][key] = value;
        await Provider.SetPluginConfigurationAsync(pluginName, key, value);
    }
    
    public async Task<Dictionary<string, string>> GetPluginConfigurationsAsync(string pluginName)
    {
        if (PluginConfigurations.TryGetValue(pluginName, out var config))
        {
            return config;
        }
        await Provider.GetPluginConfigurationsAsync();
        return PluginConfigurations.ContainsKey(pluginName) ? PluginConfigurations[pluginName] : new Dictionary<string, string>();
    }
    
    public async Task<Dictionary<string, Dictionary<string, string>>> GetEndpointPluginConfigurationsAsync(string endpoint)
    {
        var configurations = await Provider.GetPluginConfigurationsAsync();
        return configurations ?? new Dictionary<string, Dictionary<string, string>>();
    }
    
}