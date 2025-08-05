using GatewayPluginContract;
using Endpoint = GatewayPluginContract.Entities.Endpoint;

namespace Gateway;
using System.Collections.Generic;


public interface IConfigurationsProvider : IService
{
    Task InitialiseAsync (PluginManager pluginManager, IRepoFactory repoFactory);
    Task<PipeConfiguration> GetPipeConfigAsync(Endpoint? endpoint = null);
    Task<Dictionary<string, Dictionary<string, string>>> GetServiceConfigsAsync(Endpoint? endpoint = null);

    void LoadPipeConfigs();
    void LoadServiceConfigs();
}