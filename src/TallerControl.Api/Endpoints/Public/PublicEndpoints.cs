using TallerControl.Application.Common;
using TallerControl.Application.Payments;
using TallerControl.Application.PublicLinks;

namespace TallerControl.Api.Endpoints.Public;

/// <summary>
/// Endpoints públicos anónimos para presupuestos y órdenes de trabajo (doc 05 §2.8, REQ-10, REQ-14).
/// Todos emiten: Cache-Control: no-store, X-Robots-Tag: noindex, Referrer-Policy: no-referrer.
/// GET  /api/public/v1/quotes/{token}              → vista pública del presupuesto
/// POST /api/public/v1/quotes/{token}/responses    → enviar respuesta del cliente
/// GET  /api/public/v1/work-orders/{token}         → vista pública de la OT
/// GET  /api/public/v1/payments/{token}            → comprobante de pago
/// </summary>
internal static class PublicEndpoints
{
    public static IEndpointRouteBuilder MapPublicEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/public/v1/quotes/{token}",
                (string token, GetPublicQuoteHandler handler, CancellationToken ct) =>
                    GetQuoteAsync(token, handler, ct))
            .AllowAnonymous()
            .WithPublicSecurity();

        app.MapPost("/api/public/v1/quotes/{token}/responses",
                (string token, SubmitPublicResponseRequest request,
                 SubmitPublicResponseHandler handler, HttpContext ctx, CancellationToken ct) =>
                    SubmitResponseAsync(token, request, handler, ctx, ct))
            .AllowAnonymous()
            .WithPublicSecurity();

        app.MapGet("/api/public/v1/work-orders/{token}",
                (string token, GetPublicWorkOrderHandler handler, CancellationToken ct) =>
                    GetWorkOrderAsync(token, handler, ct))
            .AllowAnonymous()
            .WithPublicSecurity();

        app.MapGet("/api/public/v1/payments/{token}",
                (string token, GetPublicPaymentHandler handler, CancellationToken ct) =>
                    GetPublicPaymentAsync(token, handler, ct))
            .AllowAnonymous()
            .WithPublicSecurity();

        return app;
    }

    // Adds security headers and catches NotFoundException so headers survive the 404 response.
    private static RouteHandlerBuilder WithPublicSecurity(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter(async (ctx, next) =>
        {
            ctx.HttpContext.Response.Headers["Cache-Control"] = "no-store";
            ctx.HttpContext.Response.Headers["X-Robots-Tag"] = "noindex";
            ctx.HttpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
            try
            {
                return await next(ctx);
            }
            catch (NotFoundException)
            {
                return Results.NotFound();
            }
        });

    private static async Task<IResult> GetQuoteAsync(
        string token, GetPublicQuoteHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(token, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> SubmitResponseAsync(
        string token, SubmitPublicResponseRequest request,
        SubmitPublicResponseHandler handler, HttpContext ctx, CancellationToken ct)
    {
        var clientIp = ctx.Connection.RemoteIpAddress?.ToString();
        var userAgent = ctx.Request.Headers.UserAgent.FirstOrDefault();
        var result = await handler.HandleAsync(token, request, clientIp, userAgent, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetWorkOrderAsync(
        string token, GetPublicWorkOrderHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(token, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> GetPublicPaymentAsync(
        string token, GetPublicPaymentHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(token, ct);
        return Results.Ok(dto);
    }
}
