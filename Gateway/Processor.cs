using GatewayPluginContract;

namespace Gateway;


    
public class RequestContext
{
    public required HttpRequest Request { get; set; }
    public required HttpResponse Response { get; set; }
    public required bool IsBlocked { get; set; }
    public required string TargetPathBase { get; set; }
    public string PathPrefix { get; set; } = string.Empty;
    
    public Dictionary<string, Dictionary<string, string>> PluginConfiguration { get; set; } = new();
}


public interface IRequestProcessor : IService
{
    Task ProcessAsync(RequestContext context);

}
public class PipeProcessorContainer
{
    public GatewayPluginContract.IRequestProcessor Processor { get; set; } = null!;
    public uint Order { get; set; } = 0;
    public bool IsEnabled { get; set; } = true; // Enabled by default
}


public class RequestPipeline
{
    private List<PipeProcessorContainer> _preProcessors;
    private List<PipeProcessorContainer> _postProcessors;
    private  GatewayPluginContract.IRequestForwarder? _forwarder;
    private readonly IConfigurationsProvider? _configManager;

    public RequestPipeline( GatewayPluginContract.IRequestForwarder? forwarder,
        List<PipeProcessorContainer> preProcessors,
        List<PipeProcessorContainer> postProcessors,
        IConfigurationsProvider? configManager = null)
    {
        _preProcessors = preProcessors.OrderBy(proc => proc.Order).ToList();
        _postProcessors = postProcessors.OrderBy(proc => proc.Order).ToList();
        _forwarder = forwarder;
        _configManager = configManager;
    }

    public void SetForwarder( GatewayPluginContract.IRequestForwarder forwarder)
    {
        _forwarder = forwarder;
    }

    private void _useConfiguration(PipeConfiguration config)
    {
        _preProcessors = config.PreProcessors.Where(proc => proc.IsEnabled).OrderBy(proc => proc.Order).ToList();
        _postProcessors = config.PostProcessors.Where(proc => proc.IsEnabled).OrderBy(proc => proc.Order).ToList();
        _forwarder = config.Forwarder ?? throw new InvalidOperationException("Forwarder cannot be null in configuration.");
    }

    public async Task ProcessAsync(GatewayPluginContract.IRequestContext context)
    {
        if (_configManager != null)
        {
            // Load global configuration if available
            context.PluginConfiguration =
                await _configManager.GetServiceConfigsAsync(context.Request.Path);
            
            var config = await _configManager.GetPipeConfigsAsync(context.Request.Path);
            _useConfiguration(config);
        }
        
        var deferredTasks = new List<Func<Task>>();
        
        foreach (var processor in _preProcessors)
        {
            await processor.Processor.ProcessAsync(context, deferredTasks);
            if (context.IsBlocked)
            {
                return;
            }
        }
        
        if (_forwarder == null)
        {
            throw new InvalidOperationException("Forwarder is not set. Cannot process request without a forwarder.");
        }
        await _forwarder.ForwardAsync(context);

        foreach (var processor in _postProcessors)
        {
            await processor.Processor.ProcessAsync(context, deferredTasks);
            if (context.IsBlocked)
            {
                return;
            }
        }
        
        // Execute all deferred tasks after processing all processors
        foreach (var deferredTask in deferredTasks)
        {
            await deferredTask();
        }
    }

}

public class RequestPipelineBuilder
{
    private readonly List<PipeProcessorContainer> _preProcessors = [];
    private readonly List<PipeProcessorContainer> _postProcessors = [];
    private  GatewayPluginContract.IRequestForwarder _forwarder = null!;
    private IConfigurationsProvider? _configManager = null;

    public RequestPipelineBuilder WithForwarder( GatewayPluginContract.IRequestForwarder forwarder)
    {
        _forwarder = forwarder;
        return this;
    }

    public RequestPipelineBuilder AddPreProcessor(GatewayPluginContract.IRequestProcessor processor, uint? order = null)
    {
        AddProcessor(processor, ServiceTypes.PreProcessor, order);
        return this;
    }
    

    public RequestPipelineBuilder AddPostProcessor(GatewayPluginContract.IRequestProcessor processor, uint? order = null)
    {
        AddProcessor(processor, ServiceTypes.PostProcessor, order);
        return this;
    }
    
    public RequestPipelineBuilder AddProcessor(GatewayPluginContract.IRequestProcessor processor, ServiceTypes type, uint? order = null)
    {
        // Sets the order to the current count if not specified
        if (order is null)
        {
            order = type switch
            {
                ServiceTypes.PreProcessor => (uint)_preProcessors.Count,
                ServiceTypes.PostProcessor => (uint)_postProcessors.Count,
                _ => throw new ArgumentOutOfRangeException(nameof(type), "Invalid pipeline type")
            };
        }

        
        var container = new PipeProcessorContainer
        {
            Processor = processor,
            Order = order ?? 0, // Defaults to shut up Rider
            IsEnabled = true
        };
        
        // Check if a processor with the same order already exists
        if ((type == ServiceTypes.PreProcessor ? _preProcessors : _postProcessors).Any(proc => proc.Order == order))
        {
            throw new InvalidOperationException($"A processor with order {order} already exists in the {type} pipeline.");
        }

        // Add the processor to the appropriate list
        if (type == ServiceTypes.PreProcessor)
        {
            _preProcessors.Add(container);
        }
        else
        {
            _postProcessors.Add(container);
        }

        return this;
    }
    
    public RequestPipelineBuilder WithConfigProvider(IConfigurationsProvider manager)
    {
        _configManager = manager ?? throw new ArgumentNullException(nameof(manager), "Configuration manager cannot be null.");
        
        return this;
    }

    public RequestPipeline Build()
    {
        if (_forwarder == null && _configManager == null)
        {
            throw new InvalidOperationException("Without a config manager, a forwarder must be set before building the pipeline.");
        }
        
        // Sort the processors by their order
        _preProcessors.Sort((a, b) => a.Order.CompareTo(b.Order));
        _postProcessors.Sort((a, b) => a.Order.CompareTo(b.Order));
        
        return new RequestPipeline(_forwarder, _preProcessors, _postProcessors, _configManager);
    }
}