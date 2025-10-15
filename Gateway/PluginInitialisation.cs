using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;

namespace Gateway;

public static class PluginInitialisation
{
    public class PluginConfigManager
    {
        private HashSet<string> InitializedPlugins { get; set; } = [];
        private readonly DbContext _context;
        private readonly PluginManager _manager;

        public PluginConfigManager(DbContext context, PluginManager manager)
        {
            _context = context;
            _manager = manager;
            RefreshInitialisedPlugins();
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
                InitialisePluginIfUninitialised(plugin);
            }
        }
        
        public void InitialisePluginIfUninitialised(IPlugin plugin)
        {
            var manifest = plugin.GetManifest();
            var pluginKey = manifest.Name + "/" + manifest.Version;
            if (InitializedPlugins.Contains(pluginKey))
            {
                Console.WriteLine($"Plugin '{pluginKey}' already initialised. Skipping.");
                return;
            }
            RefreshInitialisedPlugins();
            if (InitializedPlugins.Contains(pluginKey))
            {
                return; 
            }

            Console.WriteLine($"Initialising plugin '{pluginKey}'...");

            Task AddConfig(PluginConfig conf)
            {
                _context.Set<PluginConfig>().Add(conf);
                return Task.CompletedTask;
            }
            plugin.InitialiseServiceConfiguration(_context, AddConfig);
            _context.SaveChanges();
            RegisterPlugin(pluginKey);
            Console.WriteLine($"Plugin '{pluginKey}' initialised.");
        }
    }
}