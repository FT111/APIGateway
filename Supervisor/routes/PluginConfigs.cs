using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.AspNetCore.Authorization;

namespace Supervisor.routes;

public class PluginConfigs
{
    public PluginConfigs(WebApplication app)
    {
        var route = app.MapGroup("/plugin-configs").RequireAuthorization();
        
        // GET /plugin-configs - Get all plugin configs
        route.MapGet("/", async (InternalTypes.Repositories.Supervisor data) =>
        {
            var pluginConfigs = await data.GetRepo<PluginConfig>().GetAllAsync();
            return Results.Ok(pluginConfigs);
        }).WithOpenApi();
        
        // GET /plugin-configs/pipe/{pipeId} - Get plugin configs for a specific pipe
        route.MapGet("/pipe/{pipeId:guid}", async (Guid pipeId, InternalTypes.Repositories.Supervisor data) =>
        {
            var repo = data.GetRepo<PluginConfig>();
            var pluginConfigs = await repo.QueryAsync(pc => pc.PipeId == pipeId);
            return Results.Ok(pluginConfigs);
        }).WithOpenApi();
        
        // GET /plugin-configs/namespace/{namespace} - Get plugin configs for a specific namespace
        route.MapGet("/namespace/{namespace}", async (string @namespace, InternalTypes.Repositories.Supervisor data) =>
        {
            var repo = data.GetRepo<PluginConfig>();
            var pluginConfigs = await repo.QueryAsync(pc => pc.Namespace == @namespace);
            return Results.Ok(pluginConfigs);
        }).WithOpenApi();
        
        // GET /plugin-configs/{namespace}/{key} - Get specific plugin config
        route.MapGet("/{namespace}/{key}", async (string @namespace, string key, InternalTypes.Repositories.Supervisor data) =>
        {
            var repo = data.GetRepo<PluginConfig>();
            var pluginConfigList = await repo.QueryAsync(pc => pc.Namespace == @namespace && pc.Key == key);
            var pluginConfig = pluginConfigList.FirstOrDefault();
            return pluginConfig != null ? Results.Ok(pluginConfig) : Results.NotFound();
        }).WithOpenApi();
        
        // POST /plugin-configs - Create new plugin config
        route.MapPost("/", async (CreatePluginConfigRequest request, InternalTypes.Repositories.Supervisor data) =>
        {
            var pluginConfig = new PluginConfig
            {
                Key = request.Key,
                Value = request.Value,
                Namespace = request.Namespace,
                PipeId = request.PipeId,
                Internal = request.Internal
            };
            
            await data.GetRepo<PluginConfig>().AddAsync(pluginConfig);
            return Results.Created($"/plugin-configs/{pluginConfig.Namespace}/{pluginConfig.Key}", pluginConfig);
        }).WithOpenApi();
        
        // PUT /plugin-configs/{namespace}/{key} - Update plugin config
        route.MapPut("/{namespace}/{key}", async (string @namespace, string key, UpdatePluginConfigRequest request, InternalTypes.Repositories.Supervisor data) =>
        {
            var repo = data.GetRepo<PluginConfig>();
            var pluginConfigList = await repo.QueryAsync(pc => pc.Namespace == @namespace && pc.Key == key);
            var pluginConfig = pluginConfigList.FirstOrDefault();
            
            if (pluginConfig == null)
                return Results.NotFound();
                
            pluginConfig.Value = request.Value ?? pluginConfig.Value;
            pluginConfig.Internal = request.Internal ?? pluginConfig.Internal;
            
            await repo.UpdateAsync(pluginConfig);
            return Results.Ok(pluginConfig);
        }).WithOpenApi();
        
        // DELETE /plugin-configs/{namespace}/{key} - Delete plugin config
        route.MapDelete("/{namespace}/{key}", async (string @namespace, string key, InternalTypes.Repositories.Supervisor data) =>
        {
            var repo = data.GetRepo<PluginConfig>();
            var pluginConfigList = await repo.QueryAsync(pc => pc.Namespace == @namespace && pc.Key == key);
            var pluginConfig = pluginConfigList.FirstOrDefault();
            
            if (pluginConfig == null)
                return Results.NotFound();
                
            await repo.RemoveAsync($"{@namespace}-{key}");
            return Results.NoContent();
        }).WithOpenApi();
    }
}

public record CreatePluginConfigRequest(
    string Key,
    string? Value,
    string Namespace,
    Guid PipeId,
    bool Internal = false
);

public record UpdatePluginConfigRequest(
    string? Value,
    bool? Internal
);