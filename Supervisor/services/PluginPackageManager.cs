using System.IO.Compression;

namespace Supervisor.services;

public class PluginPackageManager : GatewayPluginContract.IPluginPackageManager
{
    public string PluginStaticUrl { get; init; }
    public string PluginPath { get; init; }
    
    public PluginPackageManager(IConfiguration configuration)
    {
        PluginStaticUrl = configuration.GetValue<string>("StaticUrl") ?? throw new ArgumentNullException("StaticUrl configuration is required");
        PluginPath = configuration.GetValue<string>("UnpackagedPath") ?? throw new ArgumentNullException("UnpackagedPath configuration is required");
    }

    public string GetPluginStaticUrl()
    {
        return PluginStaticUrl;
    }

    public async void PackagePluginsAsync()
    {
        if (!Directory.Exists(PluginPath))
        {
            Directory.CreateDirectory(PluginPath);
        }

        var pluginDirectories = Directory.GetDirectories(PluginPath);
        foreach (var dir in pluginDirectories)
        {
            var pluginName = new DirectoryInfo(dir).Name;
            var zipPath = Path.Combine(PluginPath, $"{pluginName}.zip");
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
            ZipFile.CreateFromDirectory(dir, zipPath);
        }
    }
}