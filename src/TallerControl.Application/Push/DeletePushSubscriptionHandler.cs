namespace TallerControl.Application.Push;

public sealed class DeletePushSubscriptionHandler
{
    private readonly IPushSubscriptionRepository _repository;

    public DeletePushSubscriptionHandler(IPushSubscriptionRepository repository)
    {
        _repository = repository;
    }

    public Task HandleAsync(Guid userId, string endpoint, CancellationToken ct) =>
        _repository.DeleteAsync(userId, endpoint, ct);
}
