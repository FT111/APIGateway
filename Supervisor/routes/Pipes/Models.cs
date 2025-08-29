using System.Linq.Expressions;
using GatewayPluginContract.Attributes;
using GatewayPluginContract.Entities;
using Endpoint = GatewayPluginContract.Entities.Endpoint;

namespace Supervisor.routes.Pipes;

public static class Models
{
    public class PipeResponse
    {
        public Guid Id { get; set; }
        [Sortable]
        public DateTime CreatedAt { get; set; }
        [Sortable]
        public DateTime UpdatedAt { get; set; }
        public int ServiceCount { get; set; }
        public List<PipeEndpointResponse> Endpoints { get; set; } = null!; 
    }

    public class PipeEndpointResponse
    {
        public Guid Id { get; set; }
        public string Path { get; set; } = null!;
        public string? TargetPathPrefix { get; set; }
        public Guid TargetId { get; set; }
    }

    // public class CreatePipeRequest
    // {
    //     public 
    // }
    
}

public static class Mapping
{
    public static readonly Expression<Func<Pipe, Models.PipeResponse>> ToResponse = pipe => new Models.PipeResponse
    {
        Id = pipe.Id,
        CreatedAt = pipe.CreatedAt,
        UpdatedAt = pipe.UpdatedAt,
        ServiceCount = pipe.PipeServices.Count,
        Endpoints = pipe.Endpoints.Select(endpoint => new Models.PipeEndpointResponse
        {
            Id = endpoint.Id,
            Path = endpoint.Path,
            TargetPathPrefix = endpoint.TargetPathPrefix,
            TargetId = endpoint.TargetId ?? Guid.Empty
        }).ToList()
    };
}

