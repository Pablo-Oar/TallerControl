using TallerControl.Application.Common;
using TallerControl.Domain.Notifications;

namespace TallerControl.Application.Notifications;

public sealed record NotificationDto(Guid Id, string Type, string Title, string? Link, bool IsRead, DateTimeOffset CreatedAt);

public sealed record NotificationsDto(int Unread, IReadOnlyList<NotificationDto> Items);

public interface INotificationRepository
{
    /// <summary>Avisos del taller dirigidos a todos o a esa membresía, los más nuevos primero.</summary>
    Task<IReadOnlyList<Notification>> ListForAsync(Guid membershipId, int take, CancellationToken ct);

    Task<int> CountUnreadForAsync(Guid membershipId, CancellationToken ct);

    Task<Notification?> FindForAsync(Guid id, Guid membershipId, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>La campanita del panel: avisos internos (llaves encontradas, recordatorios vencidos…).</summary>
public sealed class NotificationsHandler
{
    private const int PageSize = 30;

    private readonly INotificationRepository _repo;
    private readonly TimeProvider _clock;

    public NotificationsHandler(INotificationRepository repo, TimeProvider clock)
    {
        _repo = repo;
        _clock = clock;
    }

    public async Task<NotificationsDto> ListAsync(Guid membershipId, CancellationToken ct)
    {
        var items = await _repo.ListForAsync(membershipId, PageSize, ct);
        var unread = await _repo.CountUnreadForAsync(membershipId, ct);
        return new NotificationsDto(
            unread,
            items.Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Link, n.ReadAt.HasValue, n.CreatedAt)).ToList());
    }

    public async Task MarkReadAsync(Guid id, Guid membershipId, CancellationToken ct)
    {
        var notification = await _repo.FindForAsync(id, membershipId, ct)
            ?? throw new NotFoundException("El aviso no existe.");
        notification.MarkRead(_clock.GetUtcNow());
        await _repo.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(Guid membershipId, CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        foreach (var n in await _repo.ListForAsync(membershipId, 200, ct))
            n.MarkRead(now);
        await _repo.SaveChangesAsync(ct);
    }
}
