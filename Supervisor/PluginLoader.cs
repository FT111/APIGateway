using GatewayPluginContract;

namespace Supervisor;

public static class PluginLoader
{

    private static System.Collections.IEnumerable GetPluginDlls(string subDirectory)
    {
        string fullDir;
        ICollection<string> pluginDirs;
        try
        {
            fullDir = Path.Combine(AppContext.BaseDirectory, subDirectory);
            pluginDirs = Directory.GetDirectories(fullDir);
            
        }
        catch (Exception e)
        {
            subDirectory = "../../../../";
            fullDir = Path.Combine(AppContext.BaseDirectory, subDirectory);
            pluginDirs = Directory.GetDirectories(fullDir);
        }
        foreach (var dir in pluginDirs)
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
                    isUnloadable: true,
                    configure: config => config.EnableHotReload = true
                ));
        }
        
        return pluginLoaders;
    }
}