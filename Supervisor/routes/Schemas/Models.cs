namespace Supervisor.routes.Schemas;

public static class Models
{
    public class SchemaResponse
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
    
    public class SchemaEndpointResponse
    {
        public Guid Id { get; set; }
        public string Path { get; set; } = null!;
    }
    
    public class SchemaWithEndpointsResponse : SchemaResponse
    {
        public required IEnumerable<SchemaEndpointResponse> Endpoints { get; init; }
    }
    
    public class SchemaWithMinifiedEndpointsResponse : SchemaResponse
    {
        public required int EndpointCount { get; init; }
    }
}

public static class Mapping
{
    public static readonly System.Linq.Expressions.Expression<System.Func<GatewayPluginContract.Entities.Schema, Models.SchemaResponse>> ToResponse = schema => new Models.SchemaResponse
    {
        Id = schema.Id,
        Title = schema.Title,
        Description = schema.Description,
        CreatedAt = schema.CreatedAt,
        UpdatedAt = schema.UpdatedAt
    };

    public static readonly System.Linq.Expressions.Expression<System.Func<GatewayPluginContract.Entities.Schema, Models.SchemaWithEndpointsResponse>> ToWithEndpointsResponse = schema => new Models.SchemaWithEndpointsResponse
    {
        Id = schema.Id,
        Title = schema.Title,
        Description = schema.Description,
        CreatedAt = schema.CreatedAt,
        UpdatedAt = schema.UpdatedAt,
        Endpoints = schema.Endpoints.AsQueryable().Select(endpoint => new Models.SchemaEndpointResponse
        {
            Id = endpoint.Id,
            Path = endpoint.Path
        })
    };

    public static readonly System.Linq.Expressions.Expression<System.Func<GatewayPluginContract.Entities.Schema, Models.SchemaWithMinifiedEndpointsResponse>> ToWithMinifiedEndpointsResponse = schema => new Models.SchemaWithMinifiedEndpointsResponse
    {
        Id = schema.Id,
        Title = schema.Title,
        Description = schema.Description,
        CreatedAt = schema.CreatedAt,
        UpdatedAt = schema.UpdatedAt,
        EndpointCount = schema.Endpoints.Count
    };
}