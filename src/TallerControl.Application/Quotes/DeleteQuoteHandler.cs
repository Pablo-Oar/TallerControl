using TallerControl.Application.Common;

namespace TallerControl.Application.Quotes;

/// <summary>
/// Caso de uso: borrado lógico de un presupuesto en borrador (doc 05 §2.6, T-QUO-08).
/// Solo aplicable en estado Draft.
/// </summary>
public sealed class DeleteQuoteHandler
{
    private readonly IQuoteRepository _repo;
    private readonly IActorContext _actorContext;
    private readonly TimeProvider _clock;

    public DeleteQuoteHandler(IQuoteRepository repo, IActorContext actorContext, TimeProvider clock)
    {
        _repo = repo;
        _actorContext = actorContext;
        _clock = clock;
    }

    public async Task HandleAsync(Guid id, string? reason, CancellationToken ct)
    {
        var quote = await _repo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException("El presupuesto no existe.");

        var now = _clock.GetUtcNow();
        var actorId = _actorContext.ActorId;

        try
        {
            quote.Delete(reason, actorId, now);
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);
    }
}
