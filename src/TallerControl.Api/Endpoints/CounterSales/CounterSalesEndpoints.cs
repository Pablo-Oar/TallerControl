using TallerControl.Api.Identity;
using TallerControl.Application.CounterSales;
using TallerControl.Domain.Users;

namespace TallerControl.Api.Endpoints.CounterSales;

/// <summary>
/// Endpoints de ventas de mostrador (T-CSAL-01..03, REQ-E11, E11-C).
/// GET    /api/v1/counter-sales                          → counter_sales.read  (O, A)
/// POST   /api/v1/counter-sales                          → counter_sales.write (O, A)
/// GET    /api/v1/counter-sales/{id}                     → counter_sales.read  (O, A)
/// POST   /api/v1/counter-sales/{id}/confirm             → counter_sales.write (O, A)
/// POST   /api/v1/counter-sales/{id}/cancel              → counter_sales.write (O, A)
/// DELETE /api/v1/counter-sales/{id}                     → counter_sales.write (O, A)
/// </summary>
internal static class CounterSalesEndpoints
{
    public static IEndpointRouteBuilder MapCounterSalesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/counter-sales",
                (SearchCounterSalesHandler handler, CancellationToken ct,
                 string? q = null, int page = 1, int pageSize = 25) =>
                    handler.HandleAsync(q, page, pageSize, ct))
            .RequirePermission(Permission.CounterSalesRead);

        app.MapPost("/api/v1/counter-sales",
                (CreateCounterSaleRequest request, CreateCounterSaleHandler handler, CancellationToken ct) =>
                    CreateAsync(request, handler, ct))
            .RequirePermission(Permission.CounterSalesWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/v1/counter-sales/{id:guid}",
                (Guid id, GetCounterSaleHandler handler, CancellationToken ct) =>
                    GetAsync(id, handler, ct))
            .RequirePermission(Permission.CounterSalesRead);

        app.MapPost("/api/v1/counter-sales/{id:guid}/confirm",
                (Guid id, ConfirmCounterSaleRequest request, ConfirmCounterSaleHandler handler, CancellationToken ct) =>
                    ConfirmAsync(id, request, handler, ct))
            .RequirePermission(Permission.CounterSalesWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/counter-sales/{id:guid}/cancel",
                (Guid id, CancelCounterSaleHandler handler, CancellationToken ct) =>
                    CancelAsync(id, handler, ct))
            .RequirePermission(Permission.CounterSalesWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapDelete("/api/v1/counter-sales/{id:guid}",
                (Guid id, DeleteCounterSaleHandler handler, CancellationToken ct) =>
                    DeleteAsync(id, handler, ct))
            .RequirePermission(Permission.CounterSalesWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateCounterSaleRequest request, CreateCounterSaleHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(request, ct);
        return Results.Created($"/api/v1/counter-sales/{dto.Id}", dto);
    }

    private static async Task<IResult> GetAsync(
        Guid id, GetCounterSaleHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(id, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> ConfirmAsync(
        Guid id, ConfirmCounterSaleRequest request, ConfirmCounterSaleHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(id, request, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> CancelAsync(
        Guid id, CancelCounterSaleHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(id, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id, DeleteCounterSaleHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(id, ct);
        return Results.NoContent();
    }
}
