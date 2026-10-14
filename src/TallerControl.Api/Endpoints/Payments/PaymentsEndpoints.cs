using TallerControl.Api.Identity;
using TallerControl.Application.Payments;
using TallerControl.Domain.Users;

namespace TallerControl.Api.Endpoints.Payments;

/// <summary>
/// Endpoints de pagos (doc 05 §2.9, REQ-15).
/// GET  /api/v1/work-orders/{workOrderId}/payments               → payments.read  (O, A)
/// POST /api/v1/work-orders/{workOrderId}/payments               → payments.write (O, A)
/// POST /api/v1/work-orders/{workOrderId}/payments/{id}/void     → payments.void  (O)
/// GET  /api/v1/work-orders/{workOrderId}/payments/{id}/pdf      → payments.read  (O, A)
/// </summary>
internal static class PaymentsEndpoints
{
    public static IEndpointRouteBuilder MapPaymentsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/work-orders/{workOrderId:guid}/payments",
                (Guid workOrderId, GetPaymentsHandler handler, CancellationToken ct) =>
                    GetAsync(workOrderId, handler, ct))
            .RequirePermission(Permission.PaymentsRead);

        app.MapPost("/api/v1/work-orders/{workOrderId:guid}/payments",
                (Guid workOrderId, CreatePaymentRequest request,
                 CreatePaymentHandler handler, HttpRequest httpRequest, CancellationToken ct) =>
                    CreateAsync(workOrderId, request, handler, httpRequest, ct))
            .RequirePermission(Permission.PaymentsWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/work-orders/{workOrderId:guid}/payments/{id:guid}/void",
                (Guid workOrderId, Guid id, VoidPaymentRequest request,
                 VoidPaymentHandler handler, CancellationToken ct) =>
                    VoidAsync(workOrderId, id, request, handler, ct))
            .RequirePermission(Permission.PaymentsVoid)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/v1/work-orders/{workOrderId:guid}/payments/{id:guid}/pdf",
                (Guid workOrderId, Guid id, GetPaymentPdfHandler handler, CancellationToken ct) =>
                    GetPdfAsync(workOrderId, id, handler, ct))
            .RequirePermission(Permission.PaymentsRead);

        return app;
    }

    private static async Task<IResult> GetAsync(
        Guid workOrderId, GetPaymentsHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(workOrderId, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> CreateAsync(
        Guid workOrderId, CreatePaymentRequest request,
        CreatePaymentHandler handler, HttpRequest httpRequest, CancellationToken ct)
    {
        var idempotencyKey = httpRequest.Headers["Idempotency-Key"].FirstOrDefault();
        var (dto, balance) = await handler.HandleAsync(workOrderId, request, idempotencyKey, ct);
        return Results.Ok(new { Payment = dto, Balance = balance });
    }

    private static async Task<IResult> VoidAsync(
        Guid workOrderId, Guid id, VoidPaymentRequest request,
        VoidPaymentHandler handler, CancellationToken ct)
    {
        _ = workOrderId; // el handler verifica la relación mediante el pago
        var dto = await handler.HandleAsync(id, request, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> GetPdfAsync(
        Guid workOrderId, Guid id, GetPaymentPdfHandler handler, CancellationToken ct)
    {
        _ = workOrderId; // validado implícitamente: el handler verifica que el pago existe
        var bytes = await handler.HandleAsync(id, ct);
        return Results.File(bytes, "application/pdf", $"comprobante-{id:N}.pdf");
    }
}
