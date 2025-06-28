namespace Gateway;
    
public class RequestContext
{
    public required HttpRequest Request { get; set; }
    public required HttpResponse Response { get; set; }
    public required bool IsBlocked { get; set; }
}

public interface IService
{
    
}

public interface IRequestProcessor : IService
{
    Task ProcessAsync(RequestContext context);

}
public interface IRequestPipeProcessor : IRequestProcessor
{
    int Order { get; }
}


public interface IRequestForwarder : IService
{
    Task ForwardAsync(RequestContext context);
}


public class RequestPipeline
{
    private readonly List<IRequestPipeProcessor> _preProcessors;
    private readonly List<IRequestPipeProcessor> _postProcessors;
    private IRequestForwarder _forwarder;

    public RequestPipeline(IRequestForwarder forwarder,
        List<IRequestPipeProcessor> preProcessors,
        List<IRequestPipeProcessor> postProcessors)
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
            await processor.ProcessAsync(context);
            if (context.IsBlocked)
            {
                return;
            }
        }

        await _forwarder.ForwardAsync(context);

        foreach (var processor in _postProcessors)
        {
            await processor.ProcessAsync(context);
            if (context.IsBlocked)
            {
                return;
            }
        }
        
    }

}

public class RequestPipelineBuilder

{
    private readonly List<IRequestProcessor> _preProcessors = [];
    private readonly List<IRequestProcessor> _postProcessors = [];
    private IRequestForwarder _forwarder;

    public RequestPipelineBuilder WithForwarder(IRequestForwarder forwarder)
    {
        _forwarder = forwarder;
        return this;
    }

    public RequestPipelineBuilder AddPreProcessor(IRequestProcessor processor)
    {
        _preProcessors.Add(processor);
        return this;
    }

    public RequestPipelineBuilder AddPostProcessor(IRequestProcessor processor)
    {
        _postProcessors.Add(processor);
        return this;
    }

    public RequestPipeline Build()
    {
        
        if (_forwarder == null)
        {
            throw new InvalidOperationException("Forwarder must be set before building the pipeline.");
        }
        return new RequestPipeline(_forwarder, _preProcessors, _postProcessors);
    }
}