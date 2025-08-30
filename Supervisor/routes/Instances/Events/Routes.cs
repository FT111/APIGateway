using GatewayPluginContract;
using GatewayPluginContract.Entities;

namespace Supervisor.routes.Instances.Events;

public class Routes
{
    public Routes(RouteGroupBuilder instances)
    {
        var events = instances.MapGroup("/events").RequireAuthorization();

        events.MapPost("/",
            async (HttpContext context, Models.EventRequest e, SupervisorAdapter mqHandler,
                Utils.ResponseStructure<Models.EventRequest> res) =>
            {
                try
                {
                    await mqHandler.SendEventAsync(new SupervisorEvent
                    {
                        Type = Enum.Parse<SupervisorEventType>(e.Type, true),
                        Value = e.Value
                    });
                }
                catch (ArgumentException)
                {
                    return Results.BadRequest($"Invalid event name: {e.Type}");
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message, statusCode: 500);
                }

                return Results.Created($"/instances/events", res.WithData(e));
            });
    }
}