using Gateway.services;
using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;
using Endpoint = GatewayPluginContract.Entities.Endpoint;
using IRouter = GatewayPluginContract.IRouter;


namespace Gateway
{


    public class RouteTrie : IRouteTrie
    {
        private RouteNode _root = new RouteNode { Segment = "" };

        public void Insert(string path, Endpoint endpoint,
            Dictionary<string, Dictionary<string, string>> collatedPluginConfigs)
        {
            var segments = path.Trim('/').Split('/');
            var currentNode = _root;

            foreach (var segment in segments)
            {
                if (!currentNode.Children.ContainsKey(segment))
                {
                    currentNode.Children[segment] = new RouteNode { Segment = segment };
                }

                currentNode = currentNode.Children[segment];
            }

            if (currentNode.Endpoint != null)
            {
                throw new InvalidOperationException($"Duplicate endpoint registered at {path}");
            }

            currentNode.Endpoint = endpoint;
            currentNode.RoutedPluginConfs = collatedPluginConfigs;
            currentNode.Target = endpoint.Target ?? endpoint.Deployment.Target;
        }

        public RouteNode? FindClosest(string path)
        {
            var segments = path.Trim('/').Split('/');
            path = path.EndsWith("/") ? path.TrimEnd('/') : path; // Normalize path
            RouteNode currentNode = _root;

            foreach (var segment in segments)
            {
                if (!currentNode.Children.TryGetValue(segment, out var child))
                {

                    return currentNode;
                }

                if (child.Endpoint != null)
                {
                    currentNode = child; // Update to the last node with an endpoint
                }
            }


            return currentNode;
        }
    }

    public class RouterFactory(DbContext context, IConfigurationsProvider configProvider, ILogger? logger = null) : IRouterFactory
    {
        private readonly DbContext _context = context ?? throw new ArgumentNullException(nameof(context));

        private readonly IConfigurationsProvider _configProvider =
            configProvider ?? throw new ArgumentNullException(nameof(configProvider));

        private ILogger? _logger = logger;
        
        public void AddLogger(ILogger logger)
        {
            _logger = logger;
        }

        private static string FindFullEndpointPath(Endpoint endpoint)
        {
            var fullPath = endpoint.Path;
            if (endpoint.Parent != null)
            {
                fullPath = FindFullEndpointPath(endpoint.Parent) + fullPath;
            }

            return fullPath;
        }

        public async Task<IRouteTrie> BuildRouteTrie()
        {
            var deployments = _context.Set<Deployment>().Include(d => d.Target)
                .Include(d => d.Endpoints).ThenInclude(e => e.Parent).ThenInclude(e => e.Pipe)
                .ThenInclude(p => p.PipeServices)
                .Include(d => d.Endpoints).ThenInclude(e => e.Parent).ThenInclude(e => e.Pipe)
                .ThenInclude(p => p.PluginConfigs);
            var globalPluginConfigs = _context.Set<PluginConfig>().Where(pc => pc.PipeId == null).ToList();
            var structuredGlobalConfs = _configProvider.ConvertPluginConfigsToDict(globalPluginConfigs);
            await deployments.LoadAsync();

            var trie = new RouteTrie();

            foreach (var deployment in deployments)
            {
                foreach (var endpoint in deployment.Endpoints)
                {
                    var fullPath = FindFullEndpointPath(endpoint);
                    try
                    {
                        var endpointCollatedConfig = endpoint.Pipe != null
                            ? CollateEndpointPluginConfigs(
                                _configProvider.ConvertPluginConfigsToDict(endpoint.Pipe.PluginConfigs),
                                structuredGlobalConfs)
                            : structuredGlobalConfs;
                        trie.Insert(fullPath, endpoint, endpointCollatedConfig);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new InvalidOperationException(
                            $"Error inserting endpoint with ID {endpoint.Id} at path '{fullPath}': {ex.Message}", ex);
                    }
                }
            }

            return trie;
        }

        public async Task<IRouter> BuildRouterAsync()
        {
            var trie = await BuildRouteTrie();
            return new Router(trie, _logger);
        }

        private static Dictionary<string, Dictionary<string, string>> CollateEndpointPluginConfigs(
            Dictionary<string, Dictionary<string, string>> endpointConfs,
            Dictionary<string, Dictionary<string, string>> globalConfs)
        {
            // Combines to a single dictionary, with endpoint config keyvals taking precedence
            endpointConfs.ToList().ForEach(kvp =>
            {
                if (!globalConfs.ContainsKey(kvp.Key))
                {
                    globalConfs[kvp.Key] = new Dictionary<string, string>();
                }

                foreach (var (key, value) in kvp.Value)
                {
                    globalConfs[kvp.Key][key] = value;
                }
            });
            return globalConfs;
        }
    }

    /// <summary>
    /// Responsible for managing the route tries that the gateway uses to route requests. Builds tries from the database on startup.
    /// Handles atomic replacement and buffering of tries.
    /// </summary>
    public class Router : IRouter
    {
        public IRouteTrie CurrentTrie { get; private set; }
        public IRouteTrie? BufferedTrie { get; private set; }
        private ILogger? _logger;

        public Router(IRouteTrie initialTrie, ILogger? logger)
        {
            CurrentTrie = initialTrie ?? throw new ArgumentNullException(nameof(initialTrie));
            _logger = logger;
        }

        public void BufferNewTrie(IRouteTrie newTrie)
        {
            BufferedTrie = newTrie ?? throw new ArgumentNullException(nameof(newTrie));
        }

        public void SwapTries()
        {
            if (BufferedTrie == null)
            {
                throw new InvalidOperationException("No buffered trie to swap in.");
            }

            CurrentTrie = BufferedTrie;
            BufferedTrie = null;
        }
        

        public void SwapTriesAtTime(DateTime swapTime)
        {
            var delay = swapTime - DateTime.UtcNow;
            if (delay <= TimeSpan.Zero)
            {
                SwapTries();
                return;
            }

            Task.Run(async () =>
            {
                await Task.Delay(delay);
                SwapTries();
                if (_logger != null)
                {
                    _logger.LogInformation($"Swapped in new route trie at {DateTime.UtcNow}");
                }
            });
        }
    }
}