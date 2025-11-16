using System.Text.RegularExpressions;

namespace GatewayPluginContract.Entities;

public partial class Event : Entity
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public Guid? Endpointid { get; set; }
    public Guid? RequestId { get; set; }
    public Guid? GroupId { get; set; }

    public string? ServiceIdentifier { get; set; }

    public bool IsDismissed { get; set; }

    public bool IsWarning { get; set; }

    public DateTime CreatedAt { get; set; }

    public string MetaType { get; set; } = null!;

    public string? MetaData { get; set; }

    public virtual Endpoint? Endpoint { get; set; }
    public virtual Request? Request { get; set; }
    public virtual RequestGroup? Group { get; set; }
}
