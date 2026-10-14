using TallerControl.Application.Common;

namespace TallerControl.Application.Work;

/// <summary>
/// Caso de uso: historial de cambios de estado de una OT (doc 05 §2.7, T-WO-14).
/// </summary>
public sealed class GetWorkOrderHistoryHandler
{
    private readonly IWorkOrderRepository _repo;

    public GetWorkOrderHistoryHandler(IWorkOrderRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<StatusChangeDto>> HandleAsync(Guid id, CancellationToken ct)
    {
        // Verificar que la OT exista (→ 404 si no).
        _ = await _repo.FindByIdAsync(id, ct)
            ?? throw new NotFoundException("La orden no existe.");

        return await _repo.GetHistoryAsync(id, ct);
    }
}
