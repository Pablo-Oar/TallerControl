namespace TallerControl.Application.Push;

public interface IPushSubscriptionRepository
{
    Task UpsertAsync(Guid userId, string endpoint, string p256Dh, string auth, CancellationToken ct);
    Task DeleteAsync(Guid userId, string endpoint, CancellationToken ct);
}
