using GatewayPluginContract;

namespace Gateway;
using System.Collections.Generic;


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