using System.Net;
using GatewayPluginContract;
using GatewayPluginContract.Entities;
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
    private readonly IBackgroundQueue _backgroundQueue;

    public RequestPipeline( GatewayPluginContract.IRequestForwarder? forwarder,
        List<PipeProcessorContainer> preProcessors,
        List<PipeProcessorContainer> postProcessors,
        IConfigurationsProvider configManager,
        Repositories repositories,
        IBackgroundQueue backgroundQueue)
    {
        _preProcessors = preProcessors.OrderBy(proc => proc.Order).ToList();
        _postProcessors = postProcessors.OrderBy(proc => proc.Order).ToList();
        _forwarder = forwarder;
        _configManager = configManager;
        _repositories = repositories;
        _backgroundQueue = backgroundQueue;
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
        };
        try
        {
            await container.Processor.Instance.ProcessAsync(context, tools);
        }
        catch (Exception ex)
        {
            // Handle processor failure based on its failure policy
            switch (container.FailurePolicy)
            {
                case ServiceFailurePolicies.Ignore:
                    Console.WriteLine($"Ignoring failure in processor {container.Identifier}: {ex.Message}");
                    break;
                case ServiceFailurePolicies.RetryThenBlock when context.RestartCount < 3:
                    Console.WriteLine($"Retrying processor {container.Identifier} due to failure: {ex.Message}");
                    context.IsRestartRequested = true;
                    break;
                case ServiceFailurePolicies.RetryThenBlock:
                    Console.WriteLine($"Blocking pipeline as  {container.Identifier} failed. failure: {ex.Message}");
                    context.IsBlocked = true;
                    break;
                case ServiceFailurePolicies.RetryThenIgnore when context.RestartCount < 3:
                    Console.WriteLine($"Retrying processor {container.Identifier} due to failure: {ex.Message}");
                    context.IsRestartRequested = true;
                    break;
                case ServiceFailurePolicies.Block:
                    Console.WriteLine($"Blocking pipeline as {container.Identifier} failed. failure: {ex.Message}");
                    context.IsBlocked = true;
                    break;
                default:
                    break;
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
            Console.WriteLine("Restart requested, reprocessing the pipeline.");
            context.RestartCount++;
            await ProcessAsync(context, httpContext);
            throw new Exceptions.PipelineEndedException("Pipeline processing gracefully restarted recursively due to request.");
        }
    }

    private void UsePipeConfig(PipeConfiguration config)
    {
        _preProcessors = config.PreProcessors.Where(proc => proc.IsEnabled).OrderBy(proc => proc.Order).ToList();
        _postProcessors = config.PostProcessors.Where(proc => proc.IsEnabled).OrderBy(proc => proc.Order).ToList();
        _forwarder = config.Forwarder ?? throw new InvalidOperationException("Forwarder cannot be null in configuration.");
    }

    public async Task ProcessAsync(GatewayPluginContract.RequestContext context, HttpContext httpContext)
    {
        try
        { 
            await SetupPipeline(context);

            foreach (var processor in _preProcessors)
            {
                Console.WriteLine($"Processing pre-processor: {processor.Identifier}");
                await UseProcessor(processor, context);
                await HandleContextRequests(context, httpContext);
            }
            
            if (_forwarder == null)
            {
                throw new InvalidOperationException("Forwarder is not set. Cannot process request without a forwarder.");
            }
            await _forwarder.ForwardAsync(context);

            foreach (var processor in _postProcessors)
            {
                Console.WriteLine($"Processing post-processor: {processor.Identifier}");
                await UseProcessor(processor, context);
                await HandleContextRequests(context, httpContext);
            }

            if (context.IsForwardingFailed)
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
            }
        }
        catch (Exceptions.PipelineEndedException)
        {
            
        }
        catch (Exception)
        {
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
            var endpoints = _repositories.GetRepo<Endpoint>();
            context.Endpoint = endpoints.QueryAsync(e => context.Request.Path.ToString().StartsWith(e.Path))
                .Result.FirstOrDefault();

            if (context.Endpoint == null)
            {
                // Use fallback target
                context.Target = _repositories.GetRepo<Target>().QueryAsync(t => t.Fallback == true)
                    .Result.FirstOrDefault() ?? throw new InvalidOperationException("No fallback target found. Cannot process request without a target.");
            }
            else
            {
                context.Target = context.Endpoint.Target;
            }
            
            // Load configuration using the endpoint object
            context.PluginConfiguration = await _configManager.GetServiceConfigsAsync(context.Endpoint);
            var config = await _configManager.GetPipeConfigAsync(context.Endpoint);
            UsePipeConfig(config); 
        }
        context.LogRequest.SourceAddress = context.Request.HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "Unknown";
        context.LogRequest.EndpointId = context.Endpoint?.Id ?? null;
    }

    private void LogRequestAsync(GatewayPluginContract.RequestContext context)
    {
        context.LogRequest.RoutedTargetId = context.Target.Id;
        context.LogRequest.HttpStatus = context.Response.StatusCode;
        async Task LogRequest(CancellationToken token, Repositories repositories)
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
    private IBackgroundQueue? _backgroundQueue = null;
    private Repositories? _repoFactory = null;
    private IConfigurationsProvider? _configManager = null;

    // public RequestPipelineBuilder WithForwarder( GatewayPluginContract.IRequestForwarder forwarder)
    // {
    //     _forwarder = forwarder;
    //     return this;
    // }
    // public RequestPipelineBuilder AddPreProcessor(GatewayPluginContract.IRequestProcessor processor, uint? order = null)
    // {
    //     AddProcessor(processor, ServiceTypes.PreProcessor, order);
    //     return this;
    // }
    //
    //
    // public RequestPipelineBuilder AddPostProcessor(GatewayPluginContract.IRequestProcessor processor, uint? order = null)
    // {
    //     AddProcessor(processor, ServiceTypes.PostProcessor, order);
    //     return this;
    // }
    //
    // public RequestPipelineBuilder AddProcessor(GatewayPluginContract.IRequestProcessor processor, ServiceTypes type, uint? order = null)
    // {
    //     // Sets the order to the current count if not specified
    //     if (order is null)
    //     {
    //         order = type switch
    //         {
    //             ServiceTypes.PreProcessor => (uint)_preProcessors.Count,
    //             ServiceTypes.PostProcessor => (uint)_postProcessors.Count,
    //             _ => throw new ArgumentOutOfRangeException(nameof(type), "Invalid pipeline type")
    //         };
    //     }
    //
    //     
    //     var container = new PipeProcessorContainer
    //     {
    //         Processor = processor,
    //         Order = order ?? 0, // Defaults to shut up Rider
    //         IsEnabled = true
    //     };
    //     
    //     // Check if a processor with the same order already exists
    //     if ((type == ServiceTypes.PreProcessor ? _preProcessors : _postProcessors).Any(proc => proc.Order == order))
    //     {
    //         throw new InvalidOperationException($"A processor with order {order} already exists in the {type} pipeline.");
    //     }
    //
    //     // Add the processor to the appropriate list
    //     if (type == ServiceTypes.PreProcessor)
    //     {
    //         _preProcessors.Add(container);
    //     }
    //     else
    //     {
    //         _postProcessors.Add(container);
    //     }
    //
    //     return this;
    // }
    
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
    
    public RequestPipelineBuilder WithBackgroundQueue(IBackgroundQueue backgroundQueue)
    {
        if (backgroundQueue == null)
        {
            throw new ArgumentNullException(nameof(backgroundQueue), "Background queue cannot be null.");
        }
        
        _backgroundQueue = backgroundQueue;
        return this;
    }

    public RequestPipeline Build()
    {
        if (_configManager == null || _repoFactory == null || _backgroundQueue == null)
        {
            throw new InvalidOperationException("All required components (config manager, store, background queue) must be set before building the pipeline.");
        }
        
        // Sort the processors by their order
        _preProcessors.Sort((a, b) => a.Order.CompareTo(b.Order));
        _postProcessors.Sort((a, b) => a.Order.CompareTo(b.Order));
        
        return new RequestPipeline(_forwarder, _preProcessors, _postProcessors, _configManager, _repoFactory, _backgroundQueue);
    }
}
