namespace  Lecti;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GatewayPluginContract;

/// <summary>
/// A/B testing plugin for Gateway
/// </summary>

public class LectiPlugin : IPlugin
{
    private PluginManifest _manifest = new PluginManifest
    {
        Name = "Lecti",
        Version = "0.1",
        Description = "A/B testing plugin for Gateway",
        Author = "FT111",
    };

    public PluginManifest GetManifest()
    {
        return _manifest;
    }

    public void ConfigureRegistrar(IPluginServiceRegistrar registrar)
    {
        registrar.RegisterService<LectiSelector>(this, new LectiSelector(), ServiceTypes.PreProcessor);
    }
    
    public Dictionary<ServiceTypes, IService[]> GetServices()
    {
        return new Dictionary<ServiceTypes, IService[]>
        {
            { ServiceTypes.PreProcessor, Array.Empty<IService>() },
            { ServiceTypes.PostProcessor, Array.Empty<IService>() },
            { ServiceTypes.Forwarder, Array.Empty<IService>() }
        };
    }
}