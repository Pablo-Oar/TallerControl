namespace TallerControl.Application.Push;

public sealed class SavePushSubscriptionHandler
{
    private readonly IPushSubscriptionRepository _repository;

    public SavePushSubscriptionHandler(IPushSubscriptionRepository repository)
    {
        _repository = repository;
    }

    public Task HandleAsync(Guid userId, PushSubscriptionRequest request, CancellationToken ct) =>
        _repository.UpsertAsync(userId, request.Endpoint, request.P256Dh, request.Auth, ct);
}
