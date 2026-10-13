using TallerControl.Application.Common;

namespace TallerControl.Application.Catalog;

/// <summary>Caso de uso: libro de movimientos de stock (doc 05 §2.5, T-STK-02).</summary>
public sealed class GetStockMovementsHandler
{
    private readonly ICatalogRepository _repo;

    public GetStockMovementsHandler(ICatalogRepository repo) => _repo = repo;

    public async Task<List<StockMovementDto>> HandleAsync(Guid catalogItemId, int page, int pageSize, CancellationToken ct)
    {
        var item = await _repo.FindAnyByIdAsync(catalogItemId, ct)
            ?? throw new NotFoundException();

        if (!item.TrackStock)
            throw new BusinessRuleException("Solo los ítems con seguimiento de stock tienen movimientos.");

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        return await _repo.GetMovementsAsync(catalogItemId, page, pageSize, ct);
    }
}
