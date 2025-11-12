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
        
    
        var currentTime = DateTime.UtcNow;
        

        var requestTimestampString = context.SharedPluginContext["Telecti"]["RequestTimestamp"] ??
                                     throw new GatewayPluginContract.Exceptions.MissingRequiredServiceException(
                                         "Missing TelectInitialiser preprocessor in pipeline");
        
    
        var requestTimestamp = DateTime.Parse(requestTimestampString, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var timeTaken = currentTime - requestTimestamp;
        
    
        return Task.CompletedTask;
    }
}