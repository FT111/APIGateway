using System.Collections;
using GatewayPluginContract;

namespace Gateway;
using McMaster.NETCore.Plugins;
using System.Collections.Generic;
using System.Collections.Immutable;

public static class PluginLoader
{

    private static System.Collections.IEnumerable GetPluginDlls(string subDirectory)
    {
        string fullDir;
        var devEnv = true;
        ICollection<string> pluginDirs;
    
        // Check for DLLs in the given subdirectory - For Prod
        fullDir = Path.Combine(AppContext.BaseDirectory, subDirectory);
        pluginDirs = Directory.GetDirectories(fullDir);
        foreach (var p in YieldDllsInPluginCollection(pluginDirs)) yield return p;

        try
        {
            // Check for DLLs in the dev environment
            subDirectory = "../../../" + subDirectory;
            fullDir = Path.Combine(AppContext.BaseDirectory, subDirectory);
            pluginDirs = Directory.GetDirectories(fullDir);
        }
        catch (Exception e)
        {
            
            devEnv = false;
        }

        if (devEnv)
        {
            foreach (var p in YieldDllsInPluginCollection(pluginDirs)) yield return p;
        }
    }

    private static IEnumerable YieldDllsInPluginCollection(ICollection<string> pluginDirs)
    {
        foreach (var dir in pluginDirs)
        {
            List<string> potentialDlls = [];
            var dirName = Path.GetFileName(dir);
            
            // Allow for versioned directories with underscores
            if (dirName.Split("_").Length == 2)
            {
                potentialDlls.Add(Path.Combine(dir, dirName.Split("_")[0] + ".dll"));
            }
                
            potentialDlls.Add(Path.Combine(dir, dirName + ".dll"));

            foreach (var targetDll in potentialDlls)
            {
                if (File.Exists(targetDll))
                {
                    yield return targetDll;
                }
            }
        }
    }

    public static List<McMaster.NETCore.Plugins.PluginLoader> GetPluginLoaders(string subDirectory = "services\\plugins")
    {
        var pluginLoaders = new List<McMaster.NETCore.Plugins.PluginLoader>();
        foreach (string pluginDll in GetPluginDlls(subDirectory))
        {
            
            pluginLoaders.Add(
                McMaster.NETCore.Plugins.PluginLoader.CreateFromAssemblyFile(
                    pluginDll,
                    sharedTypes: new[] { typeof(IPlugin), typeof(GatewayPluginContract.IRequestProcessor), typeof( GatewayPluginContract.IRequestForwarder) },
                    isUnloadable: true,
                    configure: config => config.EnableHotReload = true
                ));
        }
        
        return pluginLoaders;
    }
}