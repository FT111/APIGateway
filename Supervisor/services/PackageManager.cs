using GatewayPluginContract;
using GatewayPluginContract.Entities;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;

namespace Supervisor.services;

public class PackageManager
{
    private readonly IConfiguration _configuration;
    private readonly InternalTypes.Repositories.Gateway _data;
    private readonly IPluginPackageManager _packager;
    private readonly SupervisorAdapter _messageAdapter;
    
    public PackageManager(IConfiguration configuration, InternalTypes.Repositories.Gateway data, IPluginPackageManager pluginPackageManager, SupervisorAdapter messageAdapter)
    {
        
        _configuration = configuration;
        _data = data;
        _packager = pluginPackageManager;
        _messageAdapter = messageAdapter;
        
        HandlePackageDeliveryRequests();
    }

    private void HandlePackageDeliveryRequests()
    {
        _messageAdapter.SubscribeAsync(SupervisorEventType.Request, async (evt) =>
        {
            if (evt.Value != "NEED_PACKAGE_URL")
            {
                return;
            }
            
            await _messageAdapter.SendEventAsync(new SupervisorEvent
            {
                Type = SupervisorEventType.Response,
                Value = _packager.GetPluginStaticUrl()
            }, null, evt.CorrelationId);
        });
    }
        
}