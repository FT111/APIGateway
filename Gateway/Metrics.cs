namespace Gateway;
using System.Diagnostics;
using System.Diagnostics.Metrics;


public static class Metrics
{
    private static readonly Meter GatewayMeter = new("Gateway", "1.0.0");
    private static long _activeRequests;

    static Metrics()
    {
        GatewayMeter.CreateObservableGauge("gateway.requests.active", () => _activeRequests, "requests", "Number of active requests currently being processed.");
    }

    private static readonly Counter<long> RequestsCounter =
        GatewayMeter.CreateCounter<long>("gateway.requests.total", "requests", "Total number of requests processed by the gateway.");

    private static readonly Counter<long> FailedRequestsCounter =
        GatewayMeter.CreateCounter<long>("gateway.requests.failed.total", "requests", "Total number of failed requests.");
    
    private static readonly Histogram<double> RequestDurationHistogram =
        GatewayMeter.CreateHistogram<double>("gateway.request.duration", "s", "The duration of gateway requests.");

    private static readonly Histogram<double> UpstreamRequestDurationHistogram =
        GatewayMeter.CreateHistogram<double>("gateway.upstream.request.duration", "s", "The duration of upstream requests forwarded by the gateway.");

    private static readonly Histogram<double> PluginExecutionDurationHistogram =
        GatewayMeter.CreateHistogram<double>("gateway.plugin.duration", "s", "The duration of individual plugin executions.");

    private static readonly Histogram<long> RequestBodySizeHistogram =
        GatewayMeter.CreateHistogram<long>("gateway.request.size", "bytes", "The size of request bodies.");

    private static readonly Histogram<long> ResponseBodySizeHistogram =
        GatewayMeter.CreateHistogram<long>("gateway.response.size", "bytes", "The size of response bodies.");

    public static void IncrementActiveRequests() => Interlocked.Increment(ref _activeRequests);
    public static void DecrementActiveRequests() => Interlocked.Decrement(ref _activeRequests);

    public static void IncrementRequests(string route, string targetId, string httpMethod)
    {
        RequestsCounter.Add(1,
            new KeyValuePair<string, object?>("gateway.route", route),
            new KeyValuePair<string, object?>("gateway.target.id", targetId),
            new KeyValuePair<string, object?>("http.method", httpMethod));
    }

    public static void IncrementFailedRequests(string route, string targetId)
    {
        FailedRequestsCounter.Add(1,
            new KeyValuePair<string, object?>("gateway.route", route),
            new KeyValuePair<string, object?>("gateway.target.id", targetId));
    }


    public static void RecordRequestDuration(double duration, string route, string targetId, int statusCode)
    {
        RequestDurationHistogram.Record(duration,
            new KeyValuePair<string, object?>("gateway.route", route),
            new KeyValuePair<string, object?>("gateway.target.id", targetId),
            new KeyValuePair<string, object?>("http.status_code", statusCode));
    }

    public static void RecordUpstreamRequestDuration(double duration, string targetId)
    {
        UpstreamRequestDurationHistogram.Record(duration,
            new KeyValuePair<string, object?>("gateway.target.id", targetId));
    }

    public static void RecordPluginDuration(double duration, string pluginName, string stage)
    {
        PluginExecutionDurationHistogram.Record(duration,
            new KeyValuePair<string, object?>("gateway.plugin.name", pluginName),
            new KeyValuePair<string, object?>("gateway.plugin.stage", stage));
    }

    public static void RecordRequestBodySize(long size, string route, string targetId)
    {
        RequestBodySizeHistogram.Record(size,
            new KeyValuePair<string, object?>("gateway.route", route),
            new KeyValuePair<string, object?>("gateway.target.id", targetId));
    }

    public static void RecordResponseBodySize(long size, string route, string targetId)
    {
        ResponseBodySizeHistogram.Record(size,
            new KeyValuePair<string, object?>("gateway.route", route),
            new KeyValuePair<string, object?>("gateway.target.id", targetId));
    }
}