using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;

namespace Supervisor.routes.Pipes;

public class  Routes
{
    public Routes(WebApplication app)
    {
        var route = app.MapGroup("/pipes").RequireAuthorization();
        
        route.MapGet("/", async (InternalTypes.Repositories.Gateway data, Utils.ResponseStructure<Models.PipeResponse> res) =>
        {
            var pipes = data.Context.Set<Pipe>()
                .Include(p => p.Endpoints)
                .Include(p => p.PipeServices)
                .AsNoTracking()
                .Select(Mapping.ToResponse);
            var paginatedPipes = await res.WithData(pipes).WithPagination();
            
            return Results.Ok(paginatedPipes);
        });
        
        route.MapGet("/{id:guid}", async (Guid id, InternalTypes.Repositories.Gateway data, Utils.ResponseStructure<Models.IndividualPipeResponse> res) =>
        {
            var pipe = await data.Context.Set<Pipe>().Include(p => p.Endpoints).Include(p => p.PipeServices).FirstOrDefaultAsync(p => p.Id == id);
            if (pipe == null) return Results.NotFound();
            var mappedResponse =  Mapping.ToIndividualResponse.Compile()(pipe);
            return Results.Ok(res.WithData(mappedResponse));
        });
        
        route.MapPost("/", async (Pipe pipe, InternalTypes.Repositories.Gateway data, Utils.ResponseStructure<Models.PipeResponse> res) =>
        {
            if (pipe.Id == Guid.Empty)
            {
                pipe.Id = Guid.NewGuid();
            }
            await data.GetRepo<Pipe>().AddAsync(pipe);
            return Results.Created($"/pipes/{pipe.Id}", res.WithData(Mapping.ToResponse.Compile()(pipe)));
        });
        
        route.MapPut("/{id:guid}", async (Guid id, Pipe pipe, InternalTypes.Repositories.Supervisor data) =>
        {
            if (id != pipe.Id)
            {
                return Results.BadRequest("ID mismatch");
            }
            
            var existingPipe = await data.GetRepo<Pipe>().GetAsync(id);
            if (existingPipe == null)
            {
                return Results.NotFound();
            }

            await data.GetRepo<Pipe>().UpdateAsync(pipe);
            return Results.NoContent();
        });
        
        route.MapPut("/{id}/{serviceIdentifier:required}/configuration", async (string? id, string serviceIdentifier, Dictionary<string, string> configuration, InternalTypes.Repositories.Gateway data, PluginInitialisation.PluginConfigManager configDefManager) =>
        {
            Pipe? pipe = null;
            if (id == "global")
            {
                id = null;
            }
            else
            {
                pipe = await data.Context.Set<Pipe>().Include(pipe => pipe.PluginConfigs).FirstOrDefaultAsync(p => p.Id == Guid.Parse(id));
                if (pipe == null)
                {
                    return Results.NotFound();   
                }
            }
            
            var changes = 0;
            

            string pluginName;
            if (serviceIdentifier.Split('_').Length > 1)
            {
                pluginName = serviceIdentifier.Split('_')[0];
            }
            else
            {
                pluginName = serviceIdentifier;
            }
            
            foreach (var keyValuePair in configuration)
            {
                // Only allow updating existing configs
                // Only plugins can create new configs key/values
                if (!configDefManager.PluginConfigDefinitions.ContainsKey(serviceIdentifier))
                {
                    return Results.BadRequest("Invalid property: " + keyValuePair.Key);
                }
                var def = configDefManager.PluginConfigDefinitions[serviceIdentifier][keyValuePair.Key];
                if (def.ValueConstraint!= null)
                {
                    if (!def.ValueConstraint(keyValuePair.Value))
                    {
                        // Placeholder - Adding a error system in TODO
                        return Results.BadRequest("Invalid configuration ");
                    }
                }
                PluginConfig pluginConfig;
                try
                {
                    pluginConfig = FetchPluginConfig(pipe, keyValuePair, pluginName, data.Context);
                }
                catch (InvalidOperationException e)
                {
                    return Results.NotFound();
                }
                pluginConfig.Value = keyValuePair.Value;
                changes++;
            }

            if (changes > 0)
            {
                await data.Context.SaveChangesAsync();
            }
            
            return Results.NoContent();
        });
        
    }

    private static PluginConfig FetchPluginConfig(Pipe? pipe, KeyValuePair<string, string> keyValuePair, string pluginName, DbContext ctx)
    {
        if (pipe == null)
        {
            return ctx.Set<PluginConfig>().FirstOrDefault(p => p.Key == keyValuePair.Key && p.Namespace == pluginName && p.PipeId == null) ?? throw new InvalidOperationException();
        }
        return pipe.PluginConfigs.FirstOrDefault(pc => pc.Key == keyValuePair.Key && pc.Namespace == pluginName) ?? throw new InvalidOperationException("Plugin config not found");

    }
}