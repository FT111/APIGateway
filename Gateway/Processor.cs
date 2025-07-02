namespace Gateway;

public enum ServiceTypes
{
    PreProcessor,
    PostProcessor,
    Forwarder
}
    
public class RequestContext
{
    public required HttpRequest Request { get; set; }
    public required HttpResponse Response { get; set; }
    public required bool IsBlocked { get; set; }
    public required string TargetPathBase { get; set; }
    public string PathPrefix { get; set; } = string.Empty;
}

public interface IService
{
    
}

public interface IRequestProcessor : IService
{
    Task ProcessAsync(RequestContext context);

}
public class PipeProcessorContainer
{
    public IRequestProcessor Processor { get; set; } = null!;
    public uint Order { get; set; } = 0;
    public bool IsEnabled { get; set; } = true; // Enabled by default
}


public interface IRequestForwarder : IService
{
    Task ForwardAsync(RequestContext context);
}


public class RequestPipeline
{
    private readonly List<PipeProcessorContainer> _preProcessors;
    private readonly List<PipeProcessorContainer> _postProcessors;
    private IRequestForwarder _forwarder;

    public RequestPipeline(IRequestForwarder forwarder,
        List<PipeProcessorContainer> preProcessors,
        List<PipeProcessorContainer> postProcessors)
    {
        _preProcessors = preProcessors.OrderBy(proc => proc.Order).ToList();
        _postProcessors = postProcessors.OrderBy(proc => proc.Order).ToList();
        _forwarder = forwarder;
    }

    public void SetForwarder(IRequestForwarder forwarder)
    {
        _forwarder = forwarder;
    }

    public async Task ProcessAsync(RequestContext context)
    {
        foreach (var processor in _preProcessors)
        {
            await processor.Processor.ProcessAsync(context);
            if (context.IsBlocked)
            {
                return;
            }
        }

        await _forwarder.ForwardAsync(context);

        foreach (var processor in _postProcessors)
        {
            await processor.Processor.ProcessAsync(context);
            if (context.IsBlocked)
            {
                return;
            }
        }
        
    }

}

public class RequestPipelineBuilder
{
    private readonly List<PipeProcessorContainer> _preProcessors = [];
    private readonly List<PipeProcessorContainer> _postProcessors = [];
    private IRequestForwarder _forwarder = null!;

    public RequestPipelineBuilder WithForwarder(IRequestForwarder forwarder)
    {
        _forwarder = forwarder;
        return this;
    }

    public RequestPipelineBuilder AddPreProcessor(IRequestProcessor processor, uint? order = null)
    {
        AddProcessor(processor, ServiceTypes.PreProcessor, order);
        return this;
    }
    

    public RequestPipelineBuilder AddPostProcessor(IRequestProcessor processor, uint? order = null)
    {
        AddProcessor(processor, ServiceTypes.PostProcessor, order);
        return this;
    }
    
    public RequestPipelineBuilder AddProcessor(IRequestProcessor processor, ServiceTypes type, uint? order = null)
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
    
    public RequestPipelineBuilder FromConfiguration(PipeConfiguration configuration)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration), "Configuration cannot be null.");
        }

        _forwarder = configuration.Forwarder;

        foreach (var preProcessor in configuration.PreProcessors)
        {
            AddPreProcessor(preProcessor.Processor, preProcessor.Order);
        }

        foreach (var postProcessor in configuration.PostProcessors)
        {
            AddPostProcessor(postProcessor.Processor, postProcessor.Order);
        }

        return this;
    }

    public RequestPipeline Build()
    {
        if (_forwarder == null)
        {
            throw new InvalidOperationException("Forwarder must be set before building the pipeline.");
        }
        
        // Sort the processors by their order
        _preProcessors.Sort((a, b) => a.Order.CompareTo(b.Order));
        _postProcessors.Sort((a, b) => a.Order.CompareTo(b.Order));
        
        return new RequestPipeline(_forwarder, _preProcessors, _postProcessors);
    }
}