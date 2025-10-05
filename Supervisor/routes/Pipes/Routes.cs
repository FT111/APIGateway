using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;

namespace Supervisor.routes.Pipes;

public class Routes
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
        
        route.MapGet("/{id:guid}", async (Guid id, InternalTypes.Repositories.Gateway data, Utils.ResponseStructure<Models.PipeResponse> res) =>
        {
            var pipe = await data.GetRepo<Pipe>().GetAsync(id);
            if (pipe == null) return Results.NotFound();
            var mappedResponse =  Mapping.ToResponse.Compile()(pipe);
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
        
        route.MapPut("/{id:guid:required}/{serviceIdentifier:required}/configuration", async (Guid id, string serviceIdentifier, Dictionary<string, string> configuration, InternalTypes.Repositories.Gateway data) =>
        {
            Dictionary<string, PluginConfig> configs = data.Context.Set<PluginConfig>().Where(pc => pc.Namespace == serviceIdentifier).ToDictionary(pc => pc.Key, pc => pc);
            
            foreach (var keyValuePair in configuration)
            {
                // Only allow updating existing configs
                // Only plugins can create new configs key/values
                if (!configs.ContainsKey(keyValuePair.Key)) continue;
                configs[keyValuePair.Key].Value = keyValuePair.Value;
                data.Context.Set<PluginConfig>().Update(configs[keyValuePair.Key]);
            }
            await data.Context.SaveChangesAsync();
            
            return Results.NoContent();
        });
        
    }
}