using System.Reflection;
using GatewayPluginContract.Entities;

namespace Supervisor.routes;

public static class Utils
{
    // Injected to routes
    // Takes limit and offset from query strings, and applies them to a queryable
    public class Paginator<T> where T : class
    {
        public int Limit { get; init; }
        public int Offset { get; init; }
        public string? Query { get; init; }
        
        
        public static ValueTask<Paginator<T>> BindAsync(HttpContext context, ParameterInfo parameter)
        {
            var limit = context.Request.Query.TryGetValue("limit", out var limitValue) && int.TryParse(limitValue, out var parsedLimit) ? parsedLimit : 10;
            var offset = context.Request.Query.TryGetValue("offset", out var offsetValue) && int.TryParse(offsetValue, out var parsedOffset) ? parsedOffset : 0;
            var query = context.Request.Query.TryGetValue("query", out var queryValue) ? queryValue.ToString() : null;
            
            return ValueTask.FromResult(new Paginator<T> { Limit = limit, Offset = offset , Query = query});
        }
        
        public IQueryable<T> Apply(IQueryable<T> query)
        {
            return query.Skip(Offset).Take(Limit);
        }
    }
}