using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.AspNetCore.Authorization;

namespace Supervisor.routes;

public class Requests
{
    public Requests(WebApplication app)
    {
        var route = app.MapGroup("/requests").RequireAuthorization();
        
        // GET /requests - Get all requests with optional filtering
        route.MapGet("/", async (
            InternalTypes.Repositories.Supervisor data,
            DateTime? fromDate = null, 
            DateTime? toDate = null, 
            string? sourceAddress = null, 
            Guid? endpointId = null,
            int page = 1,
            int pageSize = 50) =>
        {
            var repo = data.GetRepo<Request>();
            var requests = await repo.GetAllAsync();
            
            var filteredRequests = requests.AsQueryable();
            
            if (fromDate.HasValue)
                filteredRequests = filteredRequests.Where(r => r.CreatedAt >= fromDate.Value);
                
            if (toDate.HasValue)
                filteredRequests = filteredRequests.Where(r => r.CreatedAt <= toDate.Value);
                
            if (!string.IsNullOrEmpty(sourceAddress))
                filteredRequests = filteredRequests.Where(r => r.SourceAddress == sourceAddress);
                
            if (endpointId.HasValue)
                filteredRequests = filteredRequests.Where(r => r.EndpointId == endpointId.Value);
            
            var orderedRequests = filteredRequests.OrderByDescending(r => r.CreatedAt);
            var pagedRequests = orderedRequests.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            
            return Results.Ok(new
            {
                Data = pagedRequests,
                Page = page,
                PageSize = pageSize,
                TotalCount = filteredRequests.Count()
            });
        }).WithOpenApi();
        
        // GET /requests/{id} - Get request by ID
        route.MapGet("/{id:guid}", async (Guid id, InternalTypes.Repositories.Supervisor data) =>
        {
            var request = await data.GetRepo<Request>().GetAsync(id);
            return request != null ? Results.Ok(request) : Results.NotFound();
        }).WithOpenApi();
        
        // GET /requests/endpoint/{endpointId} - Get requests for specific endpoint
        route.MapGet("/endpoint/{endpointId:guid}", async (
            Guid endpointId, 
            InternalTypes.Repositories.Supervisor data,
            int page = 1, 
            int pageSize = 50) =>
        {
            var repo = data.GetRepo<Request>();
            var requests = await repo.QueryAsync(r => r.EndpointId == endpointId);
            var orderedRequests = requests.OrderByDescending(r => r.CreatedAt);
            var pagedRequests = orderedRequests.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            
            return Results.Ok(new
            {
                Data = pagedRequests,
                Page = page,
                PageSize = pageSize,
                TotalCount = requests.Count()
            });
        }).WithOpenApi();
        
        // POST /requests - Create new request (typically for logging)
        route.MapPost("/", async (CreateRequestRequest request, InternalTypes.Repositories.Supervisor data) =>
        {
            var requestItem = new Request
            {
                Id = Guid.NewGuid(),
                SourceAddress = request.SourceAddress,
                EndpointId = request.EndpointId,
                CreatedAt = DateTime.UtcNow
            };
            
            await data.GetRepo<Request>().AddAsync(requestItem);
            return Results.Created($"/requests/{requestItem.Id}", requestItem);
        }).WithOpenApi();
        
        // DELETE /requests/{id} - Delete request
        route.MapDelete("/{id:guid}", async (Guid id, InternalTypes.Repositories.Supervisor data) =>
        {
            var repo = data.GetRepo<Request>();
            var request = await repo.GetAsync(id);
            
            if (request == null)
                return Results.NotFound();
                
            await repo.RemoveAsync(id.ToString());
            return Results.NoContent();
        }).WithOpenApi();
        
        // DELETE /requests/cleanup - Delete old requests (cleanup endpoint)
        route.MapDelete("/cleanup", async (
            InternalTypes.Repositories.Supervisor data,
            DateTime? olderThan = null) =>
        {
            var cutoffDate = olderThan ?? DateTime.UtcNow.AddDays(-30); // Default to 30 days
            var repo = data.GetRepo<Request>();
            var allRequests = await repo.GetAllAsync();
            var oldRequests = allRequests.Where(r => r.CreatedAt < cutoffDate).ToList();
            
            var deleteCount = 0;
            foreach (var request in oldRequests)
            {
                await repo.RemoveAsync(request.Id.ToString());
                deleteCount++;
            }
            
            return Results.Ok(new { DeletedCount = deleteCount, CutoffDate = cutoffDate });
        }).WithOpenApi();
    }
}

public record CreateRequestRequest(
    string? SourceAddress,
    Guid? EndpointId
);