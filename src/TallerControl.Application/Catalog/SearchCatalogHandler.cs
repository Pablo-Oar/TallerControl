using TallerControl.Application.Common;
using TallerControl.Domain.Catalog;

namespace TallerControl.Application.Catalog;

/// <summary>Caso de uso: buscar ítems del catálogo (doc 05 §2.5, T-CAT-01).</summary>
public sealed class SearchCatalogHandler
{
    private readonly ICatalogRepository _repo;

    public SearchCatalogHandler(ICatalogRepository repo) => _repo = repo;

    public Task<Page<CatalogItemListDto>> HandleAsync(
        string? q, CatalogItemKind? kind, bool? activeOnly, int page, int pageSize, SortSpec sort, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        return _repo.SearchAsync(q, kind, activeOnly, page, pageSize, sort, ct);
    }
}
