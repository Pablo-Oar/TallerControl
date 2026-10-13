using TallerControl.Application.Common;

namespace TallerControl.Application.Catalog;

/// <summary>Caso de uso: borrado lógico del ítem del catálogo (doc 05 §2.5, T-CAT-05).</summary>
public sealed class DeleteCatalogItemHandler
{
    private readonly ICatalogRepository _repo;
    private readonly IActorContext _actorContext;
    private readonly TimeProvider _clock;

    public DeleteCatalogItemHandler(ICatalogRepository repo, IActorContext actorContext, TimeProvider clock)
    {
        _repo = repo;
        _actorContext = actorContext;
        _clock = clock;
    }

    public async Task HandleAsync(Guid id, string? reason, CancellationToken ct)
    {
        var item = await _repo.FindActiveByIdAsync(id, ct)
            ?? throw new NotFoundException();

        item.SoftDelete(reason, _actorContext.ActorId, _clock.GetUtcNow());
        await _repo.SaveChangesAsync(ct);
    }
}
