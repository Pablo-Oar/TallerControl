using TallerControl.Api.Identity;
using TallerControl.Application.Purchases;
using TallerControl.Domain.Users;

namespace TallerControl.Api.Endpoints.Purchases;

/// <summary>
/// Endpoints de compras (T-PUR-01..05, REQ-E11, D-19).
/// GET    /api/v1/purchases                              → purchases.read  (O, A)
/// POST   /api/v1/purchases                              → purchases.write (O, A)
/// GET    /api/v1/purchases/{id}                         → purchases.read  (O, A)
/// POST   /api/v1/purchases/{id}/confirm                 → purchases.write (O, A)
/// PUT    /api/v1/purchases/{id}                         → purchases.write (O, A) [solo borrador]
/// POST   /api/v1/purchases/{id}/cancel                  → purchases.write (O, A) [motivo si estaba confirmada]
/// DELETE /api/v1/purchases/{id}                         → purchases.write (O, A)
/// </summary>
internal static class PurchasesEndpoints
{
    public static IEndpointRouteBuilder MapPurchasesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/purchases",
                (SearchPurchasesHandler handler, CancellationToken ct,
                 string? q = null, int page = 1, int pageSize = 25) =>
                    handler.HandleAsync(q, page, pageSize, ct))
            .RequirePermission(Permission.PurchasesRead);

        app.MapPost("/api/v1/purchases",
                (CreatePurchaseRequest request, CreatePurchaseHandler handler, CancellationToken ct) =>
                    CreateAsync(request, handler, ct))
            .RequirePermission(Permission.PurchasesWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/v1/purchases/{id:guid}",
                (Guid id, GetPurchaseHandler handler, CancellationToken ct) =>
                    GetAsync(id, handler, ct))
            .RequirePermission(Permission.PurchasesRead);

        app.MapPut("/api/v1/purchases/{id:guid}",
                async (Guid id, UpdatePurchaseRequest request, UpdatePurchaseHandler handler, CancellationToken ct) =>
                    Results.Ok(await handler.HandleAsync(id, request, ct)))
            .RequirePermission(Permission.PurchasesWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/purchases/{id:guid}/confirm",
                (Guid id, ConfirmPurchaseRequest request, ConfirmPurchaseHandler handler, CancellationToken ct) =>
                    ConfirmAsync(id, request, handler, ct))
            .RequirePermission(Permission.PurchasesWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/purchases/{id:guid}/cancel",
                (Guid id, CancelPurchaseRequest? request, CancelPurchaseHandler handler, CancellationToken ct) =>
                    CancelAsync(id, request?.Reason, handler, ct))
            .RequirePermission(Permission.PurchasesWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapDelete("/api/v1/purchases/{id:guid}",
                (Guid id, DeletePurchaseHandler handler, CancellationToken ct) =>
                    DeleteAsync(id, handler, ct))
            .RequirePermission(Permission.PurchasesWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreatePurchaseRequest request, CreatePurchaseHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(request, ct);
        return Results.Created($"/api/v1/purchases/{dto.Id}", dto);
    }

    private static async Task<IResult> GetAsync(
        Guid id, GetPurchaseHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(id, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> ConfirmAsync(
        Guid id, ConfirmPurchaseRequest request, ConfirmPurchaseHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(id, request, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> CancelAsync(
        Guid id, string? reason, CancelPurchaseHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(id, reason, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id, DeletePurchaseHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(id, ct);
        return Results.NoContent();
    }
}
