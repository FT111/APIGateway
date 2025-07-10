using GatewayPluginContract;

namespace Gateway;
using System.Collections.Generic;

public class PipeConfiguration
{
    public required List<PipeProcessorContainer> PreProcessors { get; set; } 
    public required List<PipeProcessorContainer> PostProcessors { get; set; }
    public required  GatewayPluginContract.IRequestForwarder? Forwarder { get; set; }
}


public interface IConfigurationsProvider
{
    Task InitialiseAsync (PluginManager pluginManager, IStore store);
    Task<Dictionary<string, PipeConfiguration>> GetAllPipeConfigsAsync();
    Task<PipeConfiguration> GetPipeConfigsAsync(string? endpoint = null);
    Task SetPipeConfigAsync(PipeConfiguration configuration, string? endpoint = null);
    Task<Dictionary<string, Dictionary<string, string>>> GetAllServiceConfigsAsync();
    Task<Dictionary<string, Dictionary<string, string>>> GetServiceConfigsAsync(string? endpoint = null);
    Task SetServiceConfigAsync(string scope, string key, string value, string? endpoint = null);
}