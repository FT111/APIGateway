using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore;

namespace Telecti;
using GatewayPluginContract;


public class Telecti : IPlugin
{
    private readonly PluginManifest _manifest = new PluginManifest
    {
        Name = "Telecti",
        Version = 0.1,
        Description = "A telemetry and observability plugin for Gateway",
        Author = "FT111",
        Dependencies = [new PluginDependency()
        {
            Name = "Lecti",
            Version = ">=0.1",
            IsOptional = true,
            VersionCheck = version => version >= 0.1
        }]
    };
    
    public void InitialiseServiceConfiguration(DbContext context, Func<PluginConfig, Task> addConfig)
    {
        
    }

    public PluginManifest GetManifest()
    {
        return _manifest;
    }

    public void ConfigurePluginRegistrar(GatewayPluginContract.IPluginServiceRegistrar registrar)
    {
        registrar.RegisterService<TelectiProcessor>(this, new TelectiProcessor(), ServiceTypes.PostProcessor);
        registrar.RegisterService<TelectiInitialiser>(this, new TelectiInitialiser(), ServiceTypes.PreProcessor);
    }
    
    public void ConfigureDataRegistrar(GatewayPluginContract.IDataRegistrar registrar)
    {
    }
    
    public Dictionary<ServiceTypes, IService[]> GetServices()
    {
        return new Dictionary<ServiceTypes, IService[]>
        {
            { ServiceTypes.PreProcessor, [] },
            { ServiceTypes.PostProcessor, [] },
            { ServiceTypes.Forwarder, [] }
        };
    }
}