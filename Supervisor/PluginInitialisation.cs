using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;

namespace Supervisor;

public static class PluginInitialisation
{
    public class PluginConfigManager
    {
        private HashSet<string> InitializedPlugins { get; set; } = [];
        public Dictionary<string, Dictionary<string, PluginConfigDefinition>> PluginConfigDefinitions { get; set; } =  new Dictionary<string, Dictionary<string, PluginConfigDefinition>>();
        private readonly DbContext _context;
        private readonly PluginManager _manager;

        public PluginConfigManager(DbContext context, PluginManager manager)
        {
            _context = context;
            _manager = manager;
            // RefreshInitialisedPlugins();
            InitialiseFromPluginManager(manager);
        }

        private void RefreshInitialisedPlugins()
        {
            InitializedPlugins = _context.Set<Plugin>().Select(p => p.Title + "/" + p.Version).ToHashSet();
        }

        private void RegisterPlugin(string identifier)
        {
            InitializedPlugins.Add(identifier);
            _context.Set<Plugin>().Add(new Plugin
            {
                Title = identifier.Split('/')[0],
                Version = identifier.Split('/')[1]
            });
            _context.SaveChanges();
        }
        
        public void InitialiseFromPluginManager(PluginManager manager)
        {
            foreach (var plugin in manager.Plugins)
            {
                InitialisePlugin(plugin);
            }
        }
        
        public void InitialisePlugin(IPlugin plugin)
        {
            var manifest = plugin.GetManifest();
            var pluginKey = manifest.Name + "/" + manifest.Version;
            List<PluginConfig> configDbObjects = [];
            
            var isPluginInitialisedInDb = InitializedPlugins.Contains(pluginKey);
            RefreshInitialisedPlugins();
            if (InitializedPlugins.Contains(pluginKey))
            {
                isPluginInitialisedInDb = true; 
            }

            

            Task AddConfig(Func<PluginConfigDefinition, PluginConfigDefinition> conf)
            {
                var def = conf(new PluginConfigDefinition(
                )
                {
                    PluginNamespace = manifest.Name
                });
                if (!PluginConfigDefinitions.TryGetValue(manifest.Name, out var val)) 
                {
                    PluginConfigDefinitions[manifest.Name] = new Dictionary<string, PluginConfigDefinition>();
                }
                PluginConfigDefinitions[manifest.Name][def.Key] = def;
                var configObj = new PluginConfig
                {
                    Key = def.Key,
                    Namespace = def.PluginNamespace,
                    PipeId = null,
                    Value = def.DefaultValue,
                    Type = def.ValueType,
                    Internal = def.Internal
                };

                if (!isPluginInitialisedInDb)
                {
                    configDbObjects.Add(configObj);
                }
                
                return Task.CompletedTask;
            }
            plugin.InitialiseServiceConfiguration(_context, AddConfig);
            if (!isPluginInitialisedInDb)
            {
                var newDefs =
                    configDbObjects.Where(config =>
                        !_context.Set<PluginConfig>().Any(exConf => exConf.Key == config.Key && exConf.Namespace==config.Namespace && exConf.PipeId == null));
                _context.Set<PluginConfig>().AddRange(newDefs);
                _context.SaveChanges();
                RegisterPlugin(pluginKey);
            }
            
        }
    }
}