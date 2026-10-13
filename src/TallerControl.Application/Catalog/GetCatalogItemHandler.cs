using TallerControl.Application.Common;

namespace TallerControl.Application.Catalog;

/// <summary>Caso de uso: obtener detalle de un ítem (doc 05 §2.5, T-CAT-01).</summary>
public sealed class GetCatalogItemHandler
{
    private readonly ICatalogRepository _repo;

    public GetCatalogItemHandler(ICatalogRepository repo) => _repo = repo;

    public async Task<CatalogItemDto> HandleAsync(Guid id, CancellationToken ct)
    {
        return await _repo.GetFullAsync(id, ct) ?? throw new NotFoundException();
    }
}
