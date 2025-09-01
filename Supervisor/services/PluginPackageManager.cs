using System.IO.Compression;

namespace Supervisor.services;

public class PluginPackageManager : GatewayPluginContract.IPluginPackageManager
{
    public string PluginStaticUrl { get; init; }
    public string UnPackagedPath { get; init; }
    public string PackagedPath { get; init; }
    
    public PluginPackageManager(IConfiguration configuration)
    {
        PluginStaticUrl = configuration.GetValue<string>("PackagesURL") ?? throw new ArgumentNullException("PackagesURL configuration is required");
        UnPackagedPath = configuration.GetValue<string>("UnpackagedPath") ?? throw new ArgumentNullException("UnpackagedPath configuration is required");
        PackagedPath = configuration.GetValue<string>("PackagedPath") ?? throw new ArgumentNullException("PackagedPath configuration is required");
    }

    public string GetPluginStaticUrl()
    {
        return PluginStaticUrl;
    }

    public async void PackagePluginsAsync()
    {
        if (!Directory.Exists(UnPackagedPath))
        {
            Directory.CreateDirectory(UnPackagedPath);
        }

        var pluginDirectories = Directory.GetDirectories(UnPackagedPath);
        foreach (var dir in pluginDirectories)
        {
            var pluginName = new DirectoryInfo(dir).Name;
            var zipPath = Path.Combine(PackagedPath, $"{pluginName}.gap");
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
            ZipFile.CreateFromDirectory(dir, zipPath);
        }
    }
}