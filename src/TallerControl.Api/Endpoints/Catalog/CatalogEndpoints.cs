using Microsoft.AspNetCore.Authorization;
using TallerControl.Api.Identity;
using TallerControl.Application.Catalog;
using TallerControl.Application.Common;
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
                 IAuthorizationService authz, HttpContext ctx,
                 string? q = null, string? kind = null, bool? activeOnly = null, bool? deleted = null,
                 int page = 1, int pageSize = 25, string? sort = null, string? dir = null) =>
                {
                    CatalogItemKind? parsedKind = kind is not null
                        && EnumQuery.TryParse<CatalogItemKind>(kind, out var k)
                        ? k : null;
                    return SearchAsync(
                        handler, authz, ctx, q, parsedKind, activeOnly, deleted, page, pageSize, SortSpec.Parse(sort, dir), ct);
                })
            .RequirePermission(Permission.CatalogRead);

        app.MapPost("/api/v1/catalog",
                (CreateCatalogItemRequest request, CreateCatalogItemHandler handler,
                 IAuthorizationService authz, HttpContext ctx, CancellationToken ct) =>
                    CreateAsync(request, handler, authz, ctx, ct))
            .RequirePermission(Permission.CatalogWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/v1/catalog/{id:guid}",
                async (Guid id, GetCatalogItemHandler handler, IAuthorizationService authz, HttpContext ctx, CancellationToken ct) =>
                {
                    var dto = await handler.HandleAsync(id, ct);
                    return await CanSeeCostsAsync(authz, ctx) ? dto : dto with { Cost = null };
                })
            .RequirePermission(Permission.CatalogRead);

        app.MapPut("/api/v1/catalog/{id:guid}",
                (Guid id, UpdateCatalogItemRequest request, UpdateCatalogItemHandler handler,
                 HttpRequest httpRequest, IAuthorizationService authz, HttpContext ctx, CancellationToken ct) =>
                    UpdateAsync(id, request, handler, httpRequest, authz, ctx, ct))
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

    /// <summary>El costo solo lo ve (y edita) quien tiene <c>costs.view</c>: para el resto no viaja en el JSON.</summary>
    private static async Task<bool> CanSeeCostsAsync(IAuthorizationService authz, HttpContext ctx) =>
        (await authz.AuthorizeAsync(ctx.User, null, $"perm:{Permission.CostsView}")).Succeeded;

    private static async Task<IResult> SearchAsync(
        SearchCatalogHandler handler, IAuthorizationService authz, HttpContext ctx,
        string? q, CatalogItemKind? kind, bool? activeOnly, bool? deleted, int page, int pageSize, SortSpec sort,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(q, kind, activeOnly, deleted, page, pageSize, sort, ct);
        if (await CanSeeCostsAsync(authz, ctx)) return Results.Ok(result);
        return Results.Ok(result with { Items = result.Items.Select(i => i with { Cost = null }).ToList() });
    }

    private static async Task<IResult> CreateAsync(
        CreateCatalogItemRequest request,
        CreateCatalogItemHandler handler,
        IAuthorizationService authz,
        HttpContext ctx,
        CancellationToken ct)
    {
        var canCost = await CanSeeCostsAsync(authz, ctx);
        var dto = await handler.HandleAsync(request, canCost, ct);
        return Results.Created($"/api/v1/catalog/{dto.Id}", canCost ? dto : dto with { Cost = null });
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateCatalogItemRequest request,
        UpdateCatalogItemHandler handler,
        HttpRequest httpRequest,
        IAuthorizationService authz,
        HttpContext ctx,
        CancellationToken ct)
    {
        if (!TryParseETag(httpRequest.Headers.IfMatch.FirstOrDefault(), out var version))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Cabecera requerida",
                detail: "Se requiere la cabecera If-Match con la versión del recurso.");
        }

        var canCost = await CanSeeCostsAsync(authz, ctx);
        var dto = await handler.HandleAsync(id, version, request, canCost, ct);
        return Results.Ok(canCost ? dto : dto with { Cost = null });
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
