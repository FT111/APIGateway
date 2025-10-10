using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Endpoint = GatewayPluginContract.Entities.Endpoint;

namespace Gateway;
using System.Collections.Generic;


public interface IConfigurationsProvider
{
    PipeConfiguration GetPipeFromDefinition(ICollection<PipeService> services);
    Dictionary<string, Dictionary<string, string>> ConvertPluginConfigsToDict(ICollection<PluginConfig>? configs);

}