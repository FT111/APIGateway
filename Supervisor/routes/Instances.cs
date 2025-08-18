using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.AspNetCore.Authorization;

namespace Supervisor.routes;

public class Instances
{
    public Instances(WebApplication app)
    {
        var route = app.MapGroup("/instances");
        var events = route.MapGroup("/events");

        events.MapPost("/{eventName}/{eventValue}",
            async (HttpContext context,string eventName, string eventValue, SupervisorAdapter mqHandler) =>
            {
                try
                {
                    await mqHandler.SendEventAsync(new SupervisorEvent
                    {
                        Type = Enum.Parse<SupervisorEventType>(eventName, true),
                        Value = eventValue
                    });
                }
                catch (ArgumentException)
                {
                    return Results.BadRequest($"Invalid event name: {eventName}");
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message, statusCode: 500);
                }
                
                return Results.Ok($"Event '{eventName.ToLower()}' with value '{eventValue}' sent successfully.");
            }).RequireAuthorization();
    }
}