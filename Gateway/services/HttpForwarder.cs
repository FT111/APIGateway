namespace Gateway.services;
using System.Net;

public class HttpRequestForwarder : GatewayPluginContract.IRequestForwarder
{
    private readonly HttpClient _client = new HttpClient();
    
    
    public async Task ForwardAsync(GatewayPluginContract.IRequestContext context)
    {
        var request = context.Request;
        var response = context.Response;
        var amendedPath = request.Path.ToString().Remove(0, context.PathPrefix.Length);

        var forwardedRequest = new HttpRequestMessage
        {
            Method = new HttpMethod(request.Method),
            RequestUri = new Uri($"{request.Scheme}://{context.TargetPathBase}{amendedPath}{request.QueryString}"),
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

        var forwardedResponse = new HttpResponseMessage();
        try 
        {
            // Forward the request to the target URL
            forwardedResponse = await _client.SendAsync(forwardedRequest);

            // Copy headers from the forwarded response to the original response
            foreach (var header in forwardedResponse.Headers)
            {
                response.Headers[header.Key] = header.Value.ToArray();
            }
            
            if (forwardedResponse.Content == null)
            {
                response.StatusCode = (int)HttpStatusCode.NoContent; // No content to forward
                return;
            }
            
            // Derive the content type from the forwarded response
            if (forwardedResponse.Content.Headers.ContentType != null)
            {
                response.ContentType = forwardedResponse.Content.Headers.ContentType.ToString();
            }
            await forwardedResponse.Content.CopyToAsync(response.Body);
            
        }
        catch (HttpRequestException e)
        {
            // Handle exceptions, e.g., log them or set a specific status code
            response.StatusCode = (int)HttpStatusCode.BadGateway;
            await response.WriteAsync($"Error forwarding request: {e.Message}");
        }
    }
}
