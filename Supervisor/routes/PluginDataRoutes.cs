using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.AspNetCore.Authorization;

namespace Supervisor.routes;

public class PluginDataRoutes
{
    public PluginDataRoutes(WebApplication app)
    {
        var route = app.MapGroup("/plugin-data").RequireAuthorization();
        
        // GET /plugin-data - Get all plugin data entries
        route.MapGet("/", async (InternalTypes.Repositories.Supervisor data) =>
        {
            var pluginData = await data.GetRepo<PluginData>().GetAllAsync();
            return Results.Ok(pluginData);
        }).WithOpenApi();
        
        // GET /plugin-data/{namespace}/{key} - Get plugin data by namespace and key
        route.MapGet("/{namespace}/{key}", async (string @namespace, string key, InternalTypes.Repositories.Supervisor data) =>
        {
            var repo = data.GetRepo<PluginData>();
            var pluginDataList = await repo.QueryAsync(pd => pd.Namespace == @namespace && pd.Key == key);
            var pluginDataItem = pluginDataList.FirstOrDefault();
            return pluginDataItem != null ? Results.Ok(pluginDataItem) : Results.NotFound();
        }).WithOpenApi();
        
        // GET /plugin-data/namespace/{namespace} - Get all plugin data for a namespace
        route.MapGet("/namespace/{namespace}", async (string @namespace, InternalTypes.Repositories.Supervisor data) =>
        {
            var repo = data.GetRepo<PluginData>();
            var pluginData = await repo.QueryAsync(pd => pd.Namespace == @namespace);
            return Results.Ok(pluginData);
        }).WithOpenApi();
        
        // POST /plugin-data - Create new plugin data entry
        route.MapPost("/", async (CreatePluginDataRequest request, InternalTypes.Repositories.Supervisor data) =>
        {
            var pluginData = new PluginData
            {
                Namespace = request.Namespace,
                Key = request.Key,
                Value = request.Value,
                Type = request.Type ?? "string",
                Category = request.Category
            };
            
            await data.GetRepo<PluginData>().AddAsync(pluginData);
            return Results.Created($"/plugin-data/{pluginData.Namespace}/{pluginData.Key}", pluginData);
        }).WithOpenApi();
        
        // PUT /plugin-data/{namespace}/{key} - Update plugin data
        route.MapPut("/{namespace}/{key}", async (string @namespace, string key, UpdatePluginDataRequest request, InternalTypes.Repositories.Supervisor data) =>
        {
            var repo = data.GetRepo<PluginData>();
            var pluginDataList = await repo.QueryAsync(pd => pd.Namespace == @namespace && pd.Key == key);
            var pluginData = pluginDataList.FirstOrDefault();
            
            if (pluginData == null)
                return Results.NotFound();
                
            pluginData.Value = request.Value ?? pluginData.Value;
            pluginData.Type = request.Type ?? pluginData.Type;
            pluginData.Category = request.Category ?? pluginData.Category;
            
            await repo.UpdateAsync(pluginData);
            return Results.Ok(pluginData);
        }).WithOpenApi();
        
        // DELETE /plugin-data/{namespace}/{key} - Delete plugin data
        route.MapDelete("/{namespace}/{key}", async (string @namespace, string key, InternalTypes.Repositories.Supervisor data) =>
        {
            var repo = data.GetRepo<PluginData>();
            var pluginDataList = await repo.QueryAsync(pd => pd.Namespace == @namespace && pd.Key == key);
            var pluginData = pluginDataList.FirstOrDefault();
            
            if (pluginData == null)
                return Results.NotFound();
                
            await repo.RemoveAsync($"{@namespace}-{key}");
            return Results.NoContent();
        }).WithOpenApi();
    }
}

public record CreatePluginDataRequest(
    string Namespace,
    string Key,
    string? Value,
    string? Type = "string",
    string? Category = null
);

public record UpdatePluginDataRequest(
    string? Value,
    string? Type,
    string? Category
);