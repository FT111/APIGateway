using System.Net;

namespace Gateway.routes;

public class HttpRequestForwarder : IRequestForwarder
{
    private readonly HttpClient _client = new HttpClient();
    
    
    public async Task ForwardAsync(RequestContext context)
    {
        var request = context.Request;
        var response = context.Response;

        var forwardedRequest = new HttpRequestMessage
        {
            Method = new HttpMethod(request.Method),
            RequestUri = new Uri($"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}"),
            Content = request.HasFormContentType
                ? new FormUrlEncodedContent(request.Form.ToDictionary(k => k.Key, v => v.Value.ToString()))
                : request.Body.CanSeek
                    ? new StreamContent(request.Body)
                    : request.Body is not null
                        ? new StreamContent(new MemoryStream())
                        : null
        };

        foreach (var header in request.Headers)
        {
            forwardedRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }

        var forwardedResponse = await _client.SendAsync(forwardedRequest);

        response.StatusCode = (int)forwardedResponse.StatusCode;
        foreach (var header in forwardedResponse.Headers)
        {
            response.Headers[header.Key] = header.Value.ToArray();
        }

        if (forwardedResponse.Content != null)
        {
            await forwardedResponse.Content.CopyToAsync(response.Body);
        }
    }
}

public static class ApiRoutes
{
    public static void Init(this WebApplication app)
    {
        const string prefix = "/api";
        var api = app.MapGroup(prefix)
            .WithOpenApi();
        
        var requestPipeline = new RequestPipelineBuilder()
            .WithForwarder(new HttpRequestForwarder())
            .Build();
        
        api.Map("/{**path}", (HttpContext context, string path) =>
                {
                    var request = context.Request;
                    var forwardedRequest = new HttpRequestMessage
                    {
                        Method = new HttpMethod(request.Method),
                        RequestUri = new Uri($"{request.Scheme}://{request.Host}{prefix}/{path}"),
                        Content = request.HasFormContentType
                            ? new FormUrlEncodedContent(request.Form.ToDictionary(k => k.Key, v => v.Value.ToString()))
                            : null
                    };
                    foreach (var header in request.Headers)
                    {
                        forwardedRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                    }
                    
                    var requestContext = new RequestContext
                    {
                        Request = request,
                        Response = context.Response,
                        IsBlocked = false
                    };
                    
                    return requestPipeline.ProcessAsync(requestContext);
                    
                    
                }
            ).WithName("ApiForwarder")
            .WithTags("Api")
            .Produces(StatusCodes.Status404NotFound);

    }
}

