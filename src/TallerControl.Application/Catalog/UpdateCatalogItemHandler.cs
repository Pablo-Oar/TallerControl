using TallerControl.Application.Common;

namespace TallerControl.Application.Catalog;

/// <summary>Caso de uso: editar ítem del catálogo con concurrencia optimista (doc 05 §2.5, T-CAT-03..04).</summary>
public sealed class UpdateCatalogItemHandler
{
    private readonly ICatalogRepository _repo;

    public UpdateCatalogItemHandler(ICatalogRepository repo) => _repo = repo;

    public async Task<CatalogItemDto> HandleAsync(
        Guid id, uint ifMatchVersion, UpdateCatalogItemRequest request, CancellationToken ct)
    {
        var item = await _repo.FindActiveByIdAsync(id, ct)
            ?? throw new NotFoundException();

        if (item.Version != ifMatchVersion)
            throw new ConcurrencyException();

        await ValidateSkuAsync(id, request.Sku, ct);

        try
        {
            item.Update(
                request.Name,
                request.Sku,
                request.NetPrice,
                request.VatRate,
                request.Cost,
                request.StockMin,
                request.DurationMinutes,
                request.BufferMinutes,
                request.IsActive);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await _repo.SaveChangesAsync(ct);

        return await _repo.GetFullAsync(id, ct)
            ?? throw new InvalidOperationException("El ítem no se pudo leer después de la edición.");
    }

    private async Task ValidateSkuAsync(Guid excludeId, string? sku, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sku)) return;

        if (await _repo.SkuExistsAsync(excludeId, sku, ct))
            throw new ConflictException("Ya existe un ítem con ese SKU.");
    }
}
