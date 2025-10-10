using Gateway.services;
using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Endpoint = GatewayPluginContract.Entities.Endpoint;


namespace Gateway
{
    public class RouteTrie
    {
        private RouteNode _root = new RouteNode { Segment = "" };

        public void Insert(string path, Endpoint endpoint, Dictionary<string, Dictionary<string, string>> collatedPluginConfigs)
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
                    Console.WriteLine($"No exact match for path '{path}'. Closest match ends at segment '{currentNode.Segment}' with endpoint {currentNode.Endpoint?.Path}");
                    return currentNode;
                }
                
                if (child.Endpoint != null)
                {
                    currentNode = child; // Update to the last node with an endpoint
                }
            }
            
            Console.WriteLine($"Exact match found for path '{path}' with endpoint {currentNode.Endpoint?.Path}");
            return currentNode;
        }
    }

    public class RouterFactory
    {
        private static string FindFullEndpointPath(Endpoint endpoint)
        {
            var fullPath = endpoint.Path;
            if (endpoint.Parent != null)
            {
                fullPath = FindFullEndpointPath(endpoint.Parent) + fullPath;
            }
            return fullPath;
        }
        public static RouteTrie BuildRouteTrie(IEnumerable<Deployment> deployments, Dictionary<string, Dictionary<string, string>> globalPluginConfigs, IConfigurationsProvider configProvider) {
            var trie = new RouteTrie();

            foreach (var deployment in deployments)
            {
                foreach (var endpoint in deployment.Endpoints)
                {
                    var fullPath = FindFullEndpointPath(endpoint);
                    try
                    {
                        var endpointCollatedConfig = endpoint.Pipe!=null ? CollateEndpointPluginConfigs(configProvider.ConvertPluginConfigsToDict(endpoint.Pipe.PluginConfigs), globalPluginConfigs) : globalPluginConfigs;
                        trie.Insert(fullPath, endpoint, endpointCollatedConfig);
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new InvalidOperationException($"Error inserting endpoint with ID {endpoint.Id} at path '{fullPath}': {ex.Message}", ex);
                    }
                }
            }

            return trie;
        }

        private static Dictionary<string, Dictionary<string, string>> CollateEndpointPluginConfigs(Dictionary<string, Dictionary<string, string>> endpointConfs, Dictionary<string, Dictionary<string, string>> globalConfs)
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
}