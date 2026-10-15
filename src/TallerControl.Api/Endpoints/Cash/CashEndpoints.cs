using TallerControl.Api.Identity;
using TallerControl.Application.Cash;
using TallerControl.Domain.Users;

namespace TallerControl.Api.Endpoints.Cash;

/// <summary>
/// Endpoints de caja (T-CAJ-01..04, REQ-E11).
/// GET    /api/v1/cash/accounts                          → cash.read  (O, A)
/// POST   /api/v1/cash/accounts                          → cash.open  (O, A)
/// GET    /api/v1/cash/accounts/{id}                     → cash.read  (O, A)
/// PUT    /api/v1/cash/accounts/{id}                     → cash.open  (O, A)
/// DELETE /api/v1/cash/accounts/{id}                     → cash.open  (O, A)
/// POST   /api/v1/cash/accounts/{id}/open                → cash.open  (O, A)
/// POST   /api/v1/cash/accounts/{id}/close               → cash.close (O, A)
/// GET    /api/v1/cash/accounts/{id}/movements           → cash.read  (O, A)
/// POST   /api/v1/cash/accounts/{id}/movements           → cash.adjust (O, A)
/// </summary>
internal static class CashEndpoints
{
    public static IEndpointRouteBuilder MapCashEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/cash/accounts",
                (SearchCashAccountsHandler handler, CancellationToken ct) =>
                    handler.HandleAsync(ct))
            .RequirePermission(Permission.CashRead);

        app.MapPost("/api/v1/cash/accounts",
                (CreateCashAccountRequest request, CreateCashAccountHandler handler, CancellationToken ct) =>
                    CreateAccountAsync(request, handler, ct))
            .RequirePermission(Permission.CashOpen)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/v1/cash/accounts/{id:guid}",
                (Guid id, GetCashAccountHandler handler, CancellationToken ct) =>
                    GetAccountAsync(id, handler, ct))
            .RequirePermission(Permission.CashRead);

        app.MapPut("/api/v1/cash/accounts/{id:guid}",
                (Guid id, UpdateCashAccountRequest request, UpdateCashAccountHandler handler, CancellationToken ct) =>
                    handler.HandleAsync(id, request, ct))
            .RequirePermission(Permission.CashOpen)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapDelete("/api/v1/cash/accounts/{id:guid}",
                (Guid id, DeleteCashAccountHandler handler, CancellationToken ct) =>
                    DeleteAccountAsync(id, handler, ct))
            .RequirePermission(Permission.CashOpen)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/cash/accounts/{id:guid}/open",
                (Guid id, OpenCashSessionRequest request, OpenCashSessionHandler handler, CancellationToken ct) =>
                    OpenSessionAsync(id, request, handler, ct))
            .RequirePermission(Permission.CashOpen)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/cash/accounts/{id:guid}/close",
                (Guid id, CloseCashSessionRequest request, CloseCashSessionHandler handler, CancellationToken ct) =>
                    CloseSessionAsync(id, request, handler, ct))
            .RequirePermission(Permission.CashClose)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/v1/cash/accounts/{id:guid}/movements",
                (Guid id, GetCashMovementsHandler handler, CancellationToken ct,
                 DateTimeOffset? from = null, DateTimeOffset? to = null) =>
                    GetMovementsAsync(id, handler, from, to, ct))
            .RequirePermission(Permission.CashRead);

        app.MapPost("/api/v1/cash/accounts/{id:guid}/movements",
                (Guid id, ManualCashMovementRequest request, ManualCashMovementHandler handler, CancellationToken ct) =>
                    AddMovementAsync(id, request, handler, ct))
            .RequirePermission(Permission.CashAdjust)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }

    private static async Task<IResult> CreateAccountAsync(
        CreateCashAccountRequest request, CreateCashAccountHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(request, ct);
        return Results.Created($"/api/v1/cash/accounts/{dto.Id}", dto);
    }

    private static async Task<IResult> GetAccountAsync(
        Guid id, GetCashAccountHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(id, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> DeleteAccountAsync(
        Guid id, DeleteCashAccountHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(id, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> OpenSessionAsync(
        Guid id, OpenCashSessionRequest request, OpenCashSessionHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(id, request, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> CloseSessionAsync(
        Guid id, CloseCashSessionRequest request, CloseCashSessionHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(id, request, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> GetMovementsAsync(
        Guid id, GetCashMovementsHandler handler,
        DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        var (movements, _) = await handler.HandleAsync(id, from, to, ct);
        return Results.Ok(movements);
    }

    private static async Task<IResult> AddMovementAsync(
        Guid id, ManualCashMovementRequest request, ManualCashMovementHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(id, request, ct);
        return Results.Created($"/api/v1/cash/accounts/{id}/movements/{dto.Id}", dto);
    }
}
