namespace Gateway;
using McMaster.NETCore.Plugins;
using System.Collections.Generic;
using System.Collections.Immutable;


public interface IPlugin
{
    public Dictionary<string, bool> GetManifest();
    
    public List<IRequestProcessor> GetPreProcessors();
    public List<IRequestProcessor> GetPostProcessors();
    public List<IRequestForwarder> GetForwarders();

    public void ConfigureRegistrar(PluginManager.PluginServiceRegistrar registrar);
}

public static class PluginLoader
{

    private static System.Collections.IEnumerable GetPluginDlls(string subDirectory)
    {
        var fullDir = Path.Combine(AppContext.BaseDirectory, subDirectory);
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

    public static List<McMaster.NETCore.Plugins.PluginLoader> GetPluginLoaders(string subDirectory = "plugins")
    {
        var pluginLoaders = new List<McMaster.NETCore.Plugins.PluginLoader>();
        foreach (string pluginDll in GetPluginDlls(subDirectory))
        {
            pluginLoaders.Add(
                McMaster.NETCore.Plugins.PluginLoader.CreateFromAssemblyFile(
                    pluginDll,
                    sharedTypes: new[] { typeof(IPlugin), typeof(IRequestProcessor), typeof(IRequestForwarder) },
                    isUnloadable: true
                ));
        }
        
        return pluginLoaders;
    }
}