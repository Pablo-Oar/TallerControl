using TallerControl.Application.Common;
using TallerControl.Domain.Catalog;

namespace TallerControl.Application.Catalog;

/// <summary>Caso de uso: crear ítem del catálogo (doc 05 §2.5, T-CAT-02).</summary>
public sealed class CreateCatalogItemHandler
{
    private readonly ICatalogRepository _repo;
    private readonly ITenantContext _tenantContext;

    public CreateCatalogItemHandler(ICatalogRepository repo, ITenantContext tenantContext)
    {
        _repo = repo;
        _tenantContext = tenantContext;
    }

    /// <param name="canEditCost">Sin permiso de costos, el costo del pedido se ignora.</param>
    public async Task<CatalogItemDto> HandleAsync(CreateCatalogItemRequest request, bool canEditCost, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        await ValidateSkuAsync(null, request.Sku, ct);

        CatalogItem item;
        try
        {
            item = new CatalogItem(
                tenantId,
                request.Kind,
                request.Name,
                request.Sku,
                request.NetPrice,
                request.VatRate,
                canEditCost ? request.Cost : null,
                request.TrackStock,
                request.StockMin,
                request.DurationMinutes,
                request.BufferMinutes,
                request.Description,
                request.StockMax);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await _repo.AddAsync(item, ct);
        await _repo.SaveChangesAsync(ct);

        return await _repo.GetFullAsync(item.Id, ct)
            ?? throw new InvalidOperationException("El ítem recién creado no se pudo leer.");
    }

    private async Task ValidateSkuAsync(Guid? excludeId, string? sku, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sku)) return;

        if (await _repo.SkuExistsAsync(excludeId, sku, ct))
            throw new ConflictException("Ya existe un ítem con ese SKU.");
    }
}
