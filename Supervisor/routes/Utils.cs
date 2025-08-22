using System.Reflection;
using GatewayPluginContract.Attributes;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;

namespace Supervisor.routes;

public static class Utils
{
    public class ResponseStructure<T> where T : class
    {
        public Dictionary<string, object> Meta { get; set; } = new();
        public IEnumerable<T> Data { get; set; } = null!;
        private Paginator<T> Paginator { get; set; } = null!;
        
        public static ValueTask<ResponseStructure<T>> BindAsync(HttpContext context, ParameterInfo parameter)
        {
            var response = new ResponseStructure<T>();
            
            var limit = context.Request.Query.TryGetValue("limit", out var limitValue) &&
                        int.TryParse(limitValue, out var parsedLimit)
                ? parsedLimit
                : 10;
            var offset =
                context.Request.Query.TryGetValue("offset", out var offsetValue) &&
                int.TryParse(offsetValue, out var parsedOffset)
                    ? parsedOffset
                    : 0;
            var sortBy = context.Request.Query.TryGetValue("sortBy", out var sortByValue)
                ? sortByValue.ToString()
                : null;
            var query = context.Request.Query.TryGetValue("query", out var queryValue) 
                ? queryValue.ToString() 
                : null;
                
            response.Paginator = new Paginator<T>(limit, offset, query, sortBy, response.Meta);
            
            return ValueTask.FromResult(response);
        }
        
        public override string ToString()
        {
            return new Dictionary<string, object>
            {
                { "meta", Meta },
                { "data", Data },
            }.ToString() ?? string.Empty;
        }
        
        public ResponseStructure<T> WithData(IEnumerable<T> data)
        {
            Data = data;
            return this;
        }

        public async Task<ResponseStructure<T>> WithPagination()
        {
            Data = await Paginator.ApplyAsync(Data.AsQueryable());
            return this;
        }
    }
    // Injected to routes
    // Takes limit and offset from query strings, and applies them to a queryable
    public class Paginator<T> where T : class
    {
        public int Limit { get; init; }
        public int Offset { get; init; }
        public string? Query { get; init; }
        private string[] QueryableAttributes { get; set; } = GetModelProperties<QueryableAttribute>(typeof(T));
        private static readonly string[] SortableAttributes = GetModelProperties<SortableAttribute>(typeof(T));
        public string SortBy { get; init; }

        public IDictionary<string, object> ResponseMeta { get; init; }
        
        public Paginator(int limit, int offset, string? query, string? sortBy, IDictionary<string, object> responseMeta)
        {
            Limit = limit;
            Offset = offset;
            Query = query;
            ResponseMeta = responseMeta;
            
            // Handle sort validation
            SortBy = string.IsNullOrEmpty(sortBy) 
                ? SortableAttributes.FirstOrDefault() ?? "CreatedAt"
                : sortBy;
                
            if (!string.IsNullOrEmpty(sortBy) && !SortableAttributes.Contains(SortBy))
            {
                throw new ArgumentException($"Cannot sort by {SortBy}. Allowed sort attributes: {string.Join(", ", SortableAttributes)}");
            }
        }
        
        public Paginator(string? sortBy)
        {
            if (string.IsNullOrEmpty(sortBy)) 
            {
                SortBy = SortableAttributes.FirstOrDefault() ?? "CreatedAt";
                return;
            }
            
            SortBy = sortBy;
            if (!SortableAttributes.Contains(SortBy))
            {
                throw new ArgumentException($"Cannot sort by {SortBy}. Allowed sort attributes: {string.Join(", ", SortableAttributes)}");
            }
        }

        // Static factory method that can be used when constructor isn't great
        public static Paginator<T> CreateFromHttpContext(HttpContext context, IDictionary<string, object> responseMeta)
        {
            var limit = context.Request.Query.TryGetValue("limit", out var limitValue) &&
                        int.TryParse(limitValue, out var parsedLimit)
                ? parsedLimit
                : 10;
            var offset =
                context.Request.Query.TryGetValue("offset", out var offsetValue) &&
                int.TryParse(offsetValue, out var parsedOffset)
                    ? parsedOffset
                    : 0;
            var sortBy = context.Request.Query.TryGetValue("sortBy", out var sortByValue)
                ? sortByValue.ToString()
                : null;
            var query = context.Request.Query.TryGetValue("query", out var queryValue) 
                ? queryValue.ToString() 
                : null;

            return new Paginator<T>(limit, offset, query, sortBy, responseMeta);
        }

        private static string[] GetModelProperties<TProperty>(Type t) where TProperty : Attribute
        {
            var properties = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var queryableProperties = new List<string>();

            foreach (var prop in properties)
            {
                if (prop.GetCustomAttribute<TProperty>() != null)
                {
                    queryableProperties.Add(prop.Name);
                }
            }

            return queryableProperties.ToArray();
        }
        
        public async Task<IEnumerable<T>> ApplyAsync(IQueryable<T> query)
        {
            IEnumerable<T> handledResponse;
            if (string.IsNullOrEmpty(Query) || QueryableAttributes.Length <= 0)
            {
                handledResponse = await ApplyPagination(ApplySorting(query)).ToListAsync();
            }
            else
            {
                handledResponse = await ApplyPagination(ApplySorting(ApplyFiltering(query))).ToListAsync();
            }
            
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalItems / Limit);
            ResponseMeta["total"] = totalItems;
            ResponseMeta["pages"] = totalPages;
            ResponseMeta["limit"] = Limit;
            ResponseMeta["offset"] = Offset;
            ResponseMeta["sort"] = SortBy;
            ResponseMeta["query"] = Query ?? string.Empty;
            
            return handledResponse;
        }
        
        public override string ToString()
        {
            return $"Paginator(Limit={Limit}, Offset={Offset}, SortBy={SortBy}, Query={Query})";
        }
        
        public IQueryable<T> ApplyPagination(IQueryable<T> query)
        {
            return query.Skip(Offset).Take(Limit);
        }
        
        public IQueryable<T> ApplyFiltering(IQueryable<T> query)
        {
            if (string.IsNullOrEmpty(Query) || QueryableAttributes.Length <= 0) return query;
            var queryLower = Query.ToLowerInvariant();
            var parameter = System.Linq.Expressions.Expression.Parameter(typeof(T), "x");
            System.Linq.Expressions.Expression? combinedExpression = null;
            foreach (var propName in QueryableAttributes)
            {
                var property = System.Linq.Expressions.Expression.Property(parameter, propName);
                System.Linq.Expressions.Expression toLowerExpression;
                if (property.Type != typeof(string))
                {
                    var propertyAsString = System.Linq.Expressions.Expression.Call(property, "ToString", null);
                    var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
                    toLowerExpression = System.Linq.Expressions.Expression.Call(propertyAsString, toLowerMethod!);
                }
                else
                {
                    var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
                    toLowerExpression = System.Linq.Expressions.Expression.Call(property, toLowerMethod!);
                }
                var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                var queryExpression = System.Linq.Expressions.Expression.Constant(queryLower);
                var containsExpression =
                    System.Linq.Expressions.Expression.Call(toLowerExpression, containsMethod!, queryExpression);
                combinedExpression = combinedExpression == null
                    ? containsExpression
                    : System.Linq.Expressions.Expression.OrElse(combinedExpression, containsExpression);
            }

            if (combinedExpression != null)
            {
                var lambda = System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(combinedExpression, parameter);
                query = query.Where(lambda);
            }

            return query;
        }
        
        public IQueryable<T> ApplySorting(IQueryable<T> query)
        {
            if (string.IsNullOrEmpty(SortBy)) return query;
            PropertyInfo? propertyInfo = null;
            try
            {
                propertyInfo = typeof(T).GetProperty(SortBy);
            }
            catch (ArgumentNullException e)
            {
                // ignored
            }

            if (propertyInfo == null) return query;
            var parameter = System.Linq.Expressions.Expression.Parameter(typeof(T), "x");
            var propertyAccess = System.Linq.Expressions.Expression.Property(parameter, propertyInfo);
            var orderByExp = System.Linq.Expressions.Expression.Lambda(propertyAccess, parameter);
            var orderByCall = System.Linq.Expressions.Expression.Call(
                typeof(Queryable),
                "OrderBy",
                new[] { typeof(T), propertyInfo.PropertyType },
                query.Expression,
                System.Linq.Expressions.Expression.Quote(orderByExp));
            query = query.Provider.CreateQuery<T>(orderByCall);

            return query;
        }
    }
}
