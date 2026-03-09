using System.Text.Json;
using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SharedServices.Commands.Internal;

public class UpdateRoutes : InternalContracts.CommandDefinition
{
    public UpdateRoutes()
    {
        PluginIdentifier = "internal";
        Identifier = "routes.update";
        Handler = async (gateway, param) => await Handle(gateway, param);
    }

    
    public async Task Handle( GatewayBase gateway, string? param)
    {
        JsonDocument paramJson;
        try
        {
            paramJson = JsonDocument.Parse(param ?? "{}");
        }
        catch (JsonException ex)
        {
            gateway.Pipe.Router.SwapTries();
            return;
        }
        
        var updateAt = paramJson.RootElement.TryGetProperty("updateAt", out var updateAtElement) && updateAtElement.ValueKind == System.Text.Json.JsonValueKind.String
            ? updateAtElement.GetString()
            : null;
        
        if (updateAt != null)
        {
            if (DateTime.TryParse(updateAt, out var updateAtDateTime))
            {
                gateway.Pipe.Router.SwapTriesAtTime(updateAtDateTime);
                return;
            }
            else
            {
                gateway.Logger?.LogError("Invalid updateAt parameter format. Expected a valid DateTime string. Falling back to immediate swap.");
                throw new ArgumentException("Invalid updateAt parameter format. Expected a valid DateTime string.");
            }
        }
    }
}