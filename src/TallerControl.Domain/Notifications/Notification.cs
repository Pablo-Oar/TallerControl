using TallerControl.Domain.Common;

namespace TallerControl.Domain.Notifications;

/// <summary>Aviso del panel. <c>MembershipId</c> nulo significa "para todos" los usuarios del taller.</summary>
public sealed class Notification : Entity, ITenantOwned
{
    private Notification()
    {
    }

    public Notification(
        Guid tenantId,
        string type,
        string title,
        DateTimeOffset createdAt,
        Guid? membershipId = null,
        string? link = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("El aviso necesita un taller.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("El aviso necesita un título.", nameof(title));
        }

        TenantId = tenantId;
        Type = type;
        Title = title;
        CreatedAt = createdAt;
        MembershipId = membershipId;
        Link = link;
    }

    public Guid TenantId { get; private set; }

    public Guid? MembershipId { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string? Link { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void MarkRead(DateTimeOffset at) => ReadAt ??= at;
}
