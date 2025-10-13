using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;

namespace Supervisor.routes.Plugins;

public class Routes
{
    public Routes(WebApplication app)
    {
        var plugins = app.MapGroup("/plugins").RequireAuthorization();

        plugins.MapGet("/",
            async (Utils.ResponseStructure<Models.PluginResponse> res,
                InternalTypes.Repositories.Gateway data) =>
            {
                var plugins = data.Context.Set<Plugin>().AsNoTracking().Select(Mapping.ToResponse).AsQueryable();
                return await res.WithData(plugins).WithPagination();

            }
        );
    }
}