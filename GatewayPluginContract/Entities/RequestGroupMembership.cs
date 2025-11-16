using GatewayPluginContract.Attributes;

namespace GatewayPluginContract.Entities;

public partial class RequestGroupMembership
{
    public Guid RequestId { get; set; }
    public Guid GroupId { get; set; }
}