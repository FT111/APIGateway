using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.AspNetCore.Authorization;

namespace Supervisor.routes;

public class Events
{
    public Events(WebApplication app)
    {
        var route = app.MapGroup("/events").RequireAuthorization();
        
        // GET /events - Get all events with optional filtering
        route.MapGet("/", async (bool? isDismissed, bool? isWarning, string? metaType, InternalTypes.Repositories.Supervisor data) =>
        {
            var repo = data.GetRepo<Event>();
            var events = await repo.GetAllAsync();
            
            var filteredEvents = events.AsQueryable();
            
            if (isDismissed.HasValue)
                filteredEvents = filteredEvents.Where(e => e.IsDismissed == isDismissed.Value);
                
            if (isWarning.HasValue)
                filteredEvents = filteredEvents.Where(e => e.IsWarning == isWarning.Value);
                
            if (!string.IsNullOrEmpty(metaType))
                filteredEvents = filteredEvents.Where(e => e.MetaType == metaType);
            
            return Results.Ok(filteredEvents.OrderByDescending(e => e.CreatedAt).ToList());
        }).WithOpenApi();
        
        // GET /events/{id} - Get event by ID
        route.MapGet("/{id:guid}", async (Guid id, InternalTypes.Repositories.Supervisor data) =>
        {
            var eventItem = await data.GetRepo<Event>().GetAsync(id);
            return eventItem != null ? Results.Ok(eventItem) : Results.NotFound();
        }).WithOpenApi();
        
        // GET /events/endpoint/{endpointId} - Get events for specific endpoint
        route.MapGet("/endpoint/{endpointId:guid}", async (Guid endpointId, InternalTypes.Repositories.Supervisor data) =>
        {
            var repo = data.GetRepo<Event>();
            var events = await repo.QueryAsync(e => e.Endpointid == endpointId);
            return Results.Ok(events.OrderByDescending(e => e.CreatedAt));
        }).WithOpenApi();
        
        // POST /events - Create new event
        route.MapPost("/", async (CreateEventRequest request, InternalTypes.Repositories.Supervisor data) =>
        {
            var eventItem = new Event
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                Description = request.Description,
                Endpointid = request.EndpointId,
                ServiceIdentifier = request.ServiceIdentifier,
                IsDismissed = false,
                IsWarning = request.IsWarning,
                CreatedAt = DateTime.UtcNow,
                MetaType = request.MetaType,
                MetaData = request.MetaData
            };
            
            await data.GetRepo<Event>().AddAsync(eventItem);
            return Results.Created($"/events/{eventItem.Id}", eventItem);
        }).WithOpenApi();
        
        // PUT /events/{id} - Update event (mainly for dismissing)
        route.MapPut("/{id:guid}", async (Guid id, UpdateEventRequest request, InternalTypes.Repositories.Supervisor data) =>
        {
            var repo = data.GetRepo<Event>();
            var eventItem = await repo.GetAsync(id);
            
            if (eventItem == null)
                return Results.NotFound();
                
            eventItem.IsDismissed = request.IsDismissed ?? eventItem.IsDismissed;
            eventItem.Description = request.Description ?? eventItem.Description;
            
            await repo.UpdateAsync(eventItem);
            return Results.Ok(eventItem);
        }).WithOpenApi();
        
        // DELETE /events/{id} - Delete event
        route.MapDelete("/{id:guid}", async (Guid id, InternalTypes.Repositories.Supervisor data) =>
        {
            var repo = data.GetRepo<Event>();
            var eventItem = await repo.GetAsync(id);
            
            if (eventItem == null)
                return Results.NotFound();
                
            await repo.RemoveAsync(id.ToString());
            return Results.NoContent();
        }).WithOpenApi();
        
        // PUT /events/{id}/dismiss - Convenience endpoint to dismiss an event
        route.MapPut("/{id:guid}/dismiss", async (Guid id, InternalTypes.Repositories.Supervisor data) =>
        {
            var repo = data.GetRepo<Event>();
            var eventItem = await repo.GetAsync(id);
            
            if (eventItem == null)
                return Results.NotFound();
                
            eventItem.IsDismissed = true;
            await repo.UpdateAsync(eventItem);
            return Results.Ok(eventItem);
        }).WithOpenApi();
    }
}

public record CreateEventRequest(
    string Title,
    string? Description,
    Guid? EndpointId,
    string? ServiceIdentifier,
    bool IsWarning,
    string MetaType,
    string? MetaData
);

public record UpdateEventRequest(
    bool? IsDismissed,
    string? Description
);