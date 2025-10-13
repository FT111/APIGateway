using GatewayPluginContract;

namespace Telecti;


public class TelectiInitialiser : IRequestProcessor
{
    public Task ProcessAsync(RequestContext context, ServiceContext stk)
    {
        // Store timestamp of the request
        context.SharedPluginContext["Telecti"] = new Dictionary<string, string>
        {
            ["RequestTimestamp"] = DateTime.UtcNow.ToString("o")
        };

        return Task.CompletedTask;
    }
}

public class TelectiProcessor : IRequestProcessor
{
    public Task ProcessAsync(RequestContext context, ServiceContext stk)
    {
        Console.WriteLine($"Processing Telecti request for {context.Request.Path} with method {context.Request.Method}");
    
        var currentTime = DateTime.UtcNow;
        Console.WriteLine(currentTime.ToString("o"));

        var requestTimestampString = context.SharedPluginContext["Telecti"]["RequestTimestamp"] ??
                                     throw new GatewayPluginContract.Exceptions.MissingRequiredServiceException(
                                         "Missing TelectInitialiser preprocessor in pipeline");
        Console.WriteLine($"Request timestamp: {requestTimestampString}");
    
        var requestTimestamp = DateTime.Parse(requestTimestampString, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var timeTaken = currentTime - requestTimestamp;
        Console.WriteLine($"Time taken for request processing: {timeTaken.TotalMilliseconds:F2} ms");
    
        return Task.CompletedTask;
    }
}