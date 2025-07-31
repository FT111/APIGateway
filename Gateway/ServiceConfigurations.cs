using GatewayPluginContract;
using Endpoint = GatewayPluginContract.Entities.Endpoint;

namespace Gateway;
using System.Collections.Generic;


public interface IConfigurationsProvider
{
    Task InitialiseAsync (PluginManager pluginManager, IRepoFactory repoFactory);
    Task<Dictionary<string, PipeConfiguration>> GetAllPipeConfigsAsync();
    Task<PipeConfiguration> GetPipeConfigAsync(Endpoint? endpoint = null);
    Task SetPipeConfigAsync(PipeConfiguration configuration, Endpoint? endpoint = null);
    Task<Dictionary<string, Dictionary<string, string>>> GetAllServiceConfigsAsync();
    Task<Dictionary<string, Dictionary<string, string>>> GetServiceConfigsAsync(Endpoint? endpoint = null);
    Task SetServiceConfigAsync(string scope, string key, string value, Endpoint? endpoint = null);
}