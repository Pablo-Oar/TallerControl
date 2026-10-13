using TallerControl.Api.Identity;
using TallerControl.Application.Catalog;
using TallerControl.Domain.Catalog;
using TallerControl.Domain.Users;

namespace TallerControl.Api.Endpoints.Catalog;

/// <summary>
/// Endpoints de catálogo y stock (doc 05 §2.5, REQ-08).
/// GET    /api/v1/catalog                              → catalog.read   (O, A, M)
/// POST   /api/v1/catalog                              → catalog.write  (O, A)
/// GET    /api/v1/catalog/{id}                         → catalog.read   (O, A, M)
/// PUT    /api/v1/catalog/{id}                         → catalog.write  (O, A)
/// DELETE /api/v1/catalog/{id}                         → catalog.write  (O, A)
/// POST   /api/v1/catalog/{id}/restore                 → catalog.write  (O, A)
/// GET    /api/v1/catalog/{id}/stock-movements         → catalog.read   (O, A)
/// POST   /api/v1/catalog/{id}/stock-adjustments       → stock.adjust   (O)
/// </summary>
internal static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/catalog",
                (SearchCatalogHandler handler, CancellationToken ct,
                 string? q = null, CatalogItemKind? kind = null, bool? activeOnly = null,
                 int page = 1, int pageSize = 25) =>
                    handler.HandleAsync(q, kind, activeOnly, page, pageSize, ct))
            .RequirePermission(Permission.CatalogRead);

        app.MapPost("/api/v1/catalog",
                (CreateCatalogItemRequest request, CreateCatalogItemHandler handler, CancellationToken ct) =>
                    CreateAsync(request, handler, ct))
            .RequirePermission(Permission.CatalogWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/v1/catalog/{id:guid}",
                (Guid id, GetCatalogItemHandler handler, CancellationToken ct) =>
                    handler.HandleAsync(id, ct))
            .RequirePermission(Permission.CatalogRead);

        app.MapPut("/api/v1/catalog/{id:guid}",
                (Guid id, UpdateCatalogItemRequest request, UpdateCatalogItemHandler handler,
                 HttpRequest httpRequest, CancellationToken ct) =>
                    UpdateAsync(id, request, handler, httpRequest, ct))
            .RequirePermission(Permission.CatalogWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapDelete("/api/v1/catalog/{id:guid}",
                (Guid id, [Microsoft.AspNetCore.Mvc.FromBody] DeleteCatalogItemRequest request,
                 DeleteCatalogItemHandler handler, CancellationToken ct) =>
                    DeleteAsync(id, request.Reason, handler, ct))
            .RequirePermission(Permission.CatalogWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/catalog/{id:guid}/restore",
                (Guid id, RestoreCatalogItemHandler handler, CancellationToken ct) =>
                    RestoreAsync(id, handler, ct))
            .RequirePermission(Permission.CatalogWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/v1/catalog/{id:guid}/stock-movements",
                (Guid id, GetStockMovementsHandler handler, CancellationToken ct,
                 int page = 1, int pageSize = 50) =>
                    handler.HandleAsync(id, page, pageSize, ct))
            .RequirePermission(Permission.CatalogRead);

        app.MapPost("/api/v1/catalog/{id:guid}/stock-adjustments",
                (Guid id, ManualStockAdjustmentRequest request,
                 ManualStockAdjustmentHandler handler, CancellationToken ct) =>
                    StockAdjustAsync(id, request, handler, ct))
            .RequirePermission(Permission.StockAdjust)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateCatalogItemRequest request,
        CreateCatalogItemHandler handler,
        CancellationToken ct)
    {
        var dto = await handler.HandleAsync(request, ct);
        return Results.Created($"/api/v1/catalog/{dto.Id}", dto);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateCatalogItemRequest request,
        UpdateCatalogItemHandler handler,
        HttpRequest httpRequest,
        CancellationToken ct)
    {
        if (!TryParseETag(httpRequest.Headers.IfMatch.FirstOrDefault(), out var version))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Cabecera requerida",
                detail: "Se requiere la cabecera If-Match con la versión del recurso.");
        }

        var dto = await handler.HandleAsync(id, version, request, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        string? reason,
        DeleteCatalogItemHandler handler,
        CancellationToken ct)
    {
        await handler.HandleAsync(id, reason, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> RestoreAsync(
        Guid id,
        RestoreCatalogItemHandler handler,
        CancellationToken ct)
    {
        var dto = await handler.HandleAsync(id, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> StockAdjustAsync(
        Guid id,
        ManualStockAdjustmentRequest request,
        ManualStockAdjustmentHandler handler,
        CancellationToken ct)
    {
        var dto = await handler.HandleAsync(id, request, ct);
        return Results.Ok(dto);
    }

    private static bool TryParseETag(string? raw, out uint version)
    {
        version = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        return uint.TryParse(raw.Trim('"'), out version);
    }
}

/// <summary>Cuerpo del DELETE (razón opcional).</summary>
internal sealed record DeleteCatalogItemRequest(string? Reason);
