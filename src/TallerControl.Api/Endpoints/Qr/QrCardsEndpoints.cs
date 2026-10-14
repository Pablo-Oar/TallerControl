using TallerControl.Api.Identity;
using TallerControl.Application.Qr;
using TallerControl.Domain.Qr;
using TallerControl.Domain.Users;

namespace TallerControl.Api.Endpoints.Qr;

/// <summary>
/// Endpoints privados de gestión de tarjetas QR (doc 05 §2.10, REQ-17).
/// GET  /api/v1/qr/cards                                → qr.manage  (O, A)
/// POST /api/v1/qr/cards/batch                          → qr.manage  (O, A)
/// GET  /api/v1/qr/cards/{id}                           → qr.manage  (O, A)
/// POST /api/v1/qr/cards/{id}/assign                    → qr.manage  (O, A)
/// POST /api/v1/qr/cards/assign-by-code                 → qr.manage  (O, A)
/// DELETE /api/v1/qr/cards/{id}/assign                  → qr.manage  (O, A) [unassign]
/// PATCH /api/v1/qr/cards/{id}/lost                     → qr.manage  (O, A)
/// POST /api/v1/qr/cards/{id}/revoke                    → qr.revoke  (O)
/// GET  /api/v1/qr/cards/{id}/print                     → qr.manage  (O, A)
/// GET  /api/v1/qr/cards/{id}/events                    → qr.manage  (O, A)
/// </summary>
internal static class QrCardsEndpoints
{
    public static IEndpointRouteBuilder MapQrCardsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/qr/cards",
                (QrCardStatus? status, string? q, int page = 1, int pageSize = 25,
                 SearchQrCardsHandler handler = default!, CancellationToken ct = default) =>
                    SearchAsync(status, q, page, pageSize, handler, ct))
            .RequirePermission(Permission.QrManage);

        app.MapPost("/api/v1/qr/cards/batch",
                (GenerateBatchRequest request, GenerateBatchHandler handler, CancellationToken ct) =>
                    GenerateBatchAsync(request, handler, ct))
            .RequirePermission(Permission.QrManage)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/v1/qr/cards/{id:guid}",
                (Guid id, GetQrCardHandler handler, CancellationToken ct) =>
                    GetCardAsync(id, handler, ct))
            .RequirePermission(Permission.QrManage);

        app.MapPost("/api/v1/qr/cards/{id:guid}/assign",
                (Guid id, AssignQrCardRequest request, AssignQrCardHandler handler, CancellationToken ct) =>
                    AssignAsync(id, request, handler, ct))
            .RequirePermission(Permission.QrManage)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/qr/cards/assign-by-code",
                (AssignByCodeRequest request, AssignByCodeHandler handler, CancellationToken ct) =>
                    AssignByCodeAsync(request, handler, ct))
            .RequirePermission(Permission.QrManage)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapDelete("/api/v1/qr/cards/{id:guid}/assign",
                (Guid id, UnassignQrCardHandler handler, CancellationToken ct) =>
                    UnassignAsync(id, handler, ct))
            .RequirePermission(Permission.QrManage)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPatch("/api/v1/qr/cards/{id:guid}/lost",
                (Guid id, MarkLostRequest request, MarkLostHandler handler, CancellationToken ct) =>
                    MarkLostAsync(id, request, handler, ct))
            .RequirePermission(Permission.QrManage)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/qr/cards/{id:guid}/revoke",
                (Guid id, RevokeQrCardRequest request, RevokeQrCardHandler handler, CancellationToken ct) =>
                    RevokeAsync(id, request, handler, ct))
            .RequirePermission(Permission.QrRevoke)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/v1/qr/cards/{id:guid}/print",
                (Guid id, string format = "card", GetQrCardPrintHandler handler = default!, CancellationToken ct = default) =>
                    PrintAsync(id, format, handler, ct))
            .RequirePermission(Permission.QrManage);

        app.MapGet("/api/v1/qr/cards/{id:guid}/events",
                (Guid id, int page = 1, int pageSize = 25,
                 GetQrCardEventsHandler handler = default!, CancellationToken ct = default) =>
                    GetEventsAsync(id, page, pageSize, handler, ct))
            .RequirePermission(Permission.QrManage);

        return app;
    }

    private static async Task<IResult> SearchAsync(
        QrCardStatus? status, string? q, int page, int pageSize,
        SearchQrCardsHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(status, q, page, pageSize, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GenerateBatchAsync(
        GenerateBatchRequest request, GenerateBatchHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetCardAsync(
        Guid id, GetQrCardHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(id, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> AssignAsync(
        Guid id, AssignQrCardRequest request, AssignQrCardHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(id, request, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> AssignByCodeAsync(
        AssignByCodeRequest request, AssignByCodeHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(request, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> UnassignAsync(
        Guid id, UnassignQrCardHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(id, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> MarkLostAsync(
        Guid id, MarkLostRequest request, MarkLostHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(id, request, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> RevokeAsync(
        Guid id, RevokeQrCardRequest request, RevokeQrCardHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(id, request, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> PrintAsync(
        Guid id, string format, GetQrCardPrintHandler handler, CancellationToken ct)
    {
        var bytes = await handler.HandleAsync(id, format, ct);
        return Results.File(bytes, "application/pdf", $"qr-card-{id:N}.pdf");
    }

    private static async Task<IResult> GetEventsAsync(
        Guid id, int page, int pageSize, GetQrCardEventsHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(id, page, pageSize, ct);
        return Results.Ok(result);
    }
}
