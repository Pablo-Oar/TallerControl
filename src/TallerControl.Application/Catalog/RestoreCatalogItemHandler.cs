using TallerControl.Application.Common;

namespace TallerControl.Application.Catalog;

/// <summary>Caso de uso: restaurar ítem del catálogo borrado (doc 05 §2.5).</summary>
public sealed class RestoreCatalogItemHandler
{
    private readonly ICatalogRepository _repo;

    public RestoreCatalogItemHandler(ICatalogRepository repo) => _repo = repo;

    public async Task<CatalogItemDto> HandleAsync(Guid id, CancellationToken ct)
    {
        var item = await _repo.FindAnyByIdAsync(id, ct)
            ?? throw new NotFoundException();

        if (!item.IsDeleted)
            throw new ConflictException("El ítem no está borrado.");

        if (item.Sku is not null && await _repo.SkuExistsAsync(id, item.Sku, ct))
            throw new ConflictException(
                "No se puede restaurar: el SKU ya está en uso por otro ítem.");

        item.Restore();
        await _repo.SaveChangesAsync(ct);

        return await _repo.GetFullAsync(id, ct)
            ?? throw new InvalidOperationException("El ítem restaurado no se pudo leer.");
    }
}
