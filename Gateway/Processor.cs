using System.Diagnostics;
using System.Net;
using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;
using Endpoint = GatewayPluginContract.Entities.Endpoint;

namespace Gateway;


public interface IRequestProcessor : IService
{
    Task ProcessAsync(RequestContext context);

}


public class RequestPipeline
{
    private List<PipeProcessorContainer> _preProcessors;
    private List<PipeProcessorContainer> _postProcessors;
    private  GatewayPluginContract.IRequestForwarder? _forwarder;
    private readonly IConfigurationsProvider? _configManager;
    private readonly Repositories _repositories;
    private readonly CacheManager _cacheManager;
    private readonly IBackgroundQueue _backgroundQueue;
    private readonly Identity.Identity _instanceIdentity;
    public RouteTrie Router;

    public RequestPipeline( GatewayPluginContract.IRequestForwarder? forwarder,
        List<PipeProcessorContainer> preProcessors,
        List<PipeProcessorContainer> postProcessors,
        IConfigurationsProvider configManager,
        Repositories repositories,
        IBackgroundQueue backgroundQueue,
        RouteTrie router,
        CacheManager cacheManager,
        Identity.Identity instanceIdentity)
    {
        _preProcessors = preProcessors.OrderBy(proc => proc.Order).ToList();
        _postProcessors = postProcessors.OrderBy(proc => proc.Order).ToList();
        _forwarder = forwarder;
        _configManager = configManager;
        _repositories = repositories;
        _backgroundQueue = backgroundQueue;
        Router = router;
        _cacheManager = cacheManager;
        _instanceIdentity = instanceIdentity;
    }

    public void SetForwarder( GatewayPluginContract.IRequestForwarder forwarder)
    {
        _forwarder = forwarder;
    }

    private async Task UseProcessor(PipeProcessorContainer container, GatewayPluginContract.RequestContext context)
    {
        var tools = new ServiceContext
        {
            DeferredTasks = _backgroundQueue,
            DataRepositories = _repositories,
            Identity = container.Processor.Identity,
            Cache = _cacheManager.GetCache(container.Processor.Identity.OriginManifest.Name),
        };

        using (context.TraceActivity = context.TraceActivity.Source.StartActivity($"Processing request: {container.Identifier}"))
        {
            try
            {
                await container.Processor.Instance.ProcessAsync(context, tools);
                context.TraceActivity.AddEvent(
                    new ActivityEvent($"Processor {container.Identifier} executed successfully"));


            }
            catch (Exception ex)
            {
                // Handle processor failure based on its failure policy
                switch (container.FailurePolicy)
                {
                    case ServiceFailurePolicies.Ignore:
                        context.TraceActivity.AddEvent(new ActivityEvent($"Processor {container.Identifier} failed (ignoring) with exception: {ex.Message}"));
                        break;
                    case ServiceFailurePolicies.RetryThenBlock when context.RestartCount < 3:
                        context.TraceActivity.AddEvent(new ActivityEvent($"Processor {container.Identifier} failed (retrying) with exception: {ex.Message}"));
                        context.IsRestartRequested = true;
                        break;
                    case ServiceFailurePolicies.RetryThenBlock:
                        context.TraceActivity.AddEvent(new ActivityEvent($"Processor {container.Identifier} failed (retrying then blocking) with exception: {ex.Message}"));

                        context.IsBlocked = true;
                        break;
                    case ServiceFailurePolicies.RetryThenIgnore when context.RestartCount < 3:
                        context.TraceActivity.AddEvent(new ActivityEvent($"Processor {container.Identifier} failed (retrying then ignoring) with exception: {ex.Message}"));

                        context.IsRestartRequested = true;
                        break;
                    case ServiceFailurePolicies.Block:
                        context.TraceActivity.AddEvent(new ActivityEvent($"Processor {container.Identifier} failed (blocking) with exception: {ex.Message}"));

                        context.IsBlocked = true;
                        break;
                    default:
                        break;
                }
            }
        }
    }

    private async Task HandleContextRequests(GatewayPluginContract.RequestContext context, HttpContext httpContext)
    {
        if (context.IsBlocked)
        {
            throw new Exceptions.PipelineBlockedException("The pipeline has been blocked by a processor.");
        }
        if (context is { IsRestartRequested: true, RestartCount: < 3 })
        {
            
            context.RestartCount++;
            context.TraceActivity.AddEvent(new ActivityEvent($"Pipeline processing restarted due to request."));
            await ProcessAsync(context, httpContext);
            throw new Exceptions.PipelineEndedException("Pipeline processing gracefully restarted recursively due to request.");
        }
    }

    private void UsePipeConfig(PipeConfiguration config)
    {
        _preProcessors = config.PreProcessors;
        _postProcessors = config.PostProcessors;
        _forwarder = config.Forwarder ?? throw new InvalidOperationException("Forwarder cannot be null in configuration.");
    }

    public async Task ProcessAsync(RequestContext context, HttpContext httpContext)
    {
        try
        {
            // Start an activity if OpenTelemetry for web framework is not enabled
            Activity.Current ??= new Activity("Gateway Pipeline").Start();
            
            Activity.Current.AddEvent(new ActivityEvent("Starting pipeline processing"));
            await SetupPipeline(context);

            foreach (var processor in _preProcessors)
            {
                await UseProcessor(processor, context);
                await HandleContextRequests(context, httpContext);
            }
            
            if (_forwarder == null)
            {
                throw new InvalidOperationException("Forwarder is not set. Cannot process request without a forwarder.");
            }

            using (context.TraceActivity = context.TraceActivity.Source.StartActivity($"Forwarding Request to {context.Target.Host} ({context.Target.Id})"))
            {
                context.TraceActivity?.AddTag("target.url", $"{context.Target.Schema}{context.Target.Host}{context.Target.BasePath}");
                context.TraceActivity?.AddTag("target.id", context.Target.Id.ToString());
                await _forwarder.ForwardAsync(context);
            }

            foreach (var processor in _postProcessors)
            {
                await UseProcessor(processor, context);
                await HandleContextRequests(context, httpContext);
            }

            if (context.IsForwardingFailed)
            {
                context.TraceActivity?.AddEvent(new ActivityEvent("Forwarding failed. Setting status code to 502 Bad Gateway."));
                context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
            }
        }
        catch (Exceptions.PipelineEndedException)
        {
            
        }
        catch (Exception e)
        {
            context.TraceActivity?.AddEvent(new ActivityEvent($"Pipeline processing failed with exception: {e.Message}"));
            context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
        }
        LogRequestAsync(context);
        
    }
    
    private async Task SetupPipeline(RequestContext context)
    {
        // Sets ephemeral state to initial values
        context.IsRestartRequested = false;
        context.IsForwardingFailed = false;
        
        
        if (_configManager != null)
        {
            // Finds the endpoint and it's routed target using the router
            // If no endpoint is found, use the fallback target
            context.Route = Router.FindClosest(context.Request.Path);
            context.Endpoint = context.Route?.Endpoint;

            if (context.Endpoint == null)
            {
                // Use fallback target
                context.Target = _repositories.GetRepo<Target>().QueryAsync(t => t.Fallback == true)
                    .Result.FirstOrDefault() ?? throw new InvalidOperationException("No fallback target found. Cannot process request without a target.");
            }
            else
            {
                context.Target = context.Route?.Target ?? throw new InvalidOperationException("No target found for the matched endpoint. Cannot process request without a target.");
            }
            
            // Load configuration using the endpoint object
            if (context.Endpoint == null)
            {
                throw new InvalidOperationException("No endpoint matched for the request path. Cannot load configuration.");
            }
            var pipeDefinition = context.Endpoint.Pipe.PipeServices;
            var pipeConfig = _configManager.GetPipeFromDefinition(pipeDefinition);
            var indexedConfigs = context.Route?.RoutedPluginConfs;
            context.PluginConfiguration = indexedConfigs;
            
            UsePipeConfig(pipeConfig); 
        }
        
        context.LogRequest.SourceAddress = context.Request.HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "Unknown";
        context.LogRequest.EndpointId = context.Endpoint?.Id ?? null;
        context.LogRequest.InstanceId = _instanceIdentity.Id;
    }

    private void LogRequestAsync(GatewayPluginContract.RequestContext context)
    {
        context.LogRequest.RoutedTargetId = context.Target.Id;
        context.LogRequest.HttpStatus = context.Response.StatusCode;
        async Task LogRequest(CancellationToken token, Repositories repositories, Activity activity, ILogger logger)
        {
            await repositories.GetRepo<Request>().AddAsync(context.LogRequest);
        }

        _backgroundQueue.QueueTask(LogRequest);
    }
}

public class RequestPipelineBuilder
{
    private readonly List<PipeProcessorContainer> _preProcessors = [];
    private readonly List<PipeProcessorContainer> _postProcessors = [];
    private  GatewayPluginContract.IRequestForwarder _forwarder = null!;
    private RouteTrie? Router { get; set; } = null;
    private IBackgroundQueue? _backgroundQueue = null;
    private CacheManager? _cacheManager = null;
    private Repositories? _repoFactory = null;
    private IConfigurationsProvider? _configManager = null;
    private Identity.Identity? _instanceIdentity = null;

    
    public RequestPipelineBuilder WithConfigProvider(IConfigurationsProvider manager)
    {
        _configManager = manager ?? throw new ArgumentNullException(nameof(manager), "Configuration manager cannot be null.");
        
        return this;
    }
    
    public RequestPipelineBuilder WithRepoProvider(Repositories repositories)
    {
        _repoFactory = repositories;
        return this;
    }
    
    public RequestPipelineBuilder WithRouter(RouteTrie router)
    {
        Router = router;
        return this;
    }
    
    public RequestPipelineBuilder WithBackgroundQueue(IBackgroundQueue backgroundQueue)
    {
        if (backgroundQueue == null)
        {
            throw new ArgumentNullException(nameof(backgroundQueue), "Background queue cannot be null.");
        }
        
        _backgroundQueue = backgroundQueue;
        return this;
    }

    public RequestPipelineBuilder WithCacheProvider(CacheManager cacheManager)
    {
        _cacheManager = cacheManager;
        return this;
    }

    public RequestPipelineBuilder WithIdentity(Identity.Identity instanceIdentity)
    {
        _instanceIdentity = instanceIdentity;
        return this;
    }

    public RequestPipeline Build()
    {
        if (_configManager == null || _repoFactory == null || _backgroundQueue == null || Router == null || _cacheManager == null
            || _instanceIdentity == null)
        {
            throw new InvalidOperationException("All required components (config manager, store, background queue) must be set before building the pipeline.");
        }
        
        // Sort the processors by their order
        _preProcessors.Sort((a, b) => a.Order.CompareTo(b.Order));
        _postProcessors.Sort((a, b) => a.Order.CompareTo(b.Order));
        
        return new RequestPipeline(_forwarder, _preProcessors, _postProcessors, _configManager, _repoFactory, _backgroundQueue, Router, _cacheManager, _instanceIdentity);
    }
}
