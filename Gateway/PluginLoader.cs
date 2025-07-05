using GatewayPluginContract;

namespace Gateway;
using McMaster.NETCore.Plugins;
using System.Collections.Generic;
using System.Collections.Immutable;

public static class PluginLoader
{

    private static System.Collections.IEnumerable GetPluginDlls(string subDirectory)
    {
        // var fullDir = Path.Combine(AppContext.BaseDirectory, subDirectory);
        var fullDir = "/Users/freddietaylor/Projects/C#Stuff/Gateway/Gateway/services/plugins";
        foreach (var dir in Directory.GetDirectories(fullDir))
        {
            var dirName = Path.GetFileName(dir);
            var targetDll = Path.Combine(dir, dirName + ".dll");

            if (File.Exists(targetDll))
            {
                yield return targetDll;
            }
        }
    }

    public static List<McMaster.NETCore.Plugins.PluginLoader> GetPluginLoaders(string subDirectory = "services/plugins")
    {
        var pluginLoaders = new List<McMaster.NETCore.Plugins.PluginLoader>();
        foreach (string pluginDll in GetPluginDlls(subDirectory))
        {
            Console.WriteLine($"Loading plugin from {pluginDll}");
            pluginLoaders.Add(
                McMaster.NETCore.Plugins.PluginLoader.CreateFromAssemblyFile(
                    pluginDll,
                    sharedTypes: new[] { typeof(IPlugin), typeof(GatewayPluginContract.IRequestProcessor), typeof( GatewayPluginContract.IRequestForwarder) },
                    isUnloadable: true
                ));
        }
        
        return pluginLoaders;
    }
}