using TallerControl.Api.Security;
using TallerControl.Application.Common;
using TallerControl.Application.Payments;
using TallerControl.Application.PublicLinks;
using TallerControl.Application.Qr;

namespace TallerControl.Api.Endpoints.Public;

/// <summary>
/// Endpoints públicos anónimos para presupuestos, órdenes de trabajo, pagos y QR (REQ-10, REQ-14, REQ-17, REQ-18).
/// Todos emiten: Cache-Control: no-store, X-Robots-Tag: noindex, Referrer-Policy: no-referrer.
/// GET  /api/public/v1/quotes/{token}                  → vista pública del presupuesto
/// POST /api/public/v1/quotes/{token}/responses        → enviar respuesta del cliente
/// GET  /api/public/v1/work-orders/{token}             → vista pública de la OT
/// GET  /api/public/v1/payments/{token}                → comprobante de pago
/// GET  /api/public/v1/qr/{code}                       → escaneo de tarjeta QR
/// POST /api/public/v1/qr/{code}/owner-challenge       → inicio de verificación del dueño
/// POST /api/public/v1/qr/{code}/owner-session         → validar OTP y obtener sesión
/// GET  /api/public/v1/qr/{code}/owner-vehicle         → portal del dueño (cookie tc_qr)
/// </summary>
internal static class PublicEndpoints
{
    private const string QrCookieName = "tc_qr";

    public static IEndpointRouteBuilder MapPublicEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/public/v1/quotes/{token}",
                (string token, GetPublicQuoteHandler handler, CancellationToken ct) =>
                    GetQuoteAsync(token, handler, ct))
            .AllowAnonymous()
            .WithPublicSecurity()
            .RequireRateLimiting(RateLimitPolicies.PublicQuote);

        app.MapPost("/api/public/v1/quotes/{token}/responses",
                (string token, SubmitPublicResponseRequest request,
                 SubmitPublicResponseHandler handler, HttpContext ctx, CancellationToken ct) =>
                    SubmitResponseAsync(token, request, handler, ctx, ct))
            .AllowAnonymous()
            .WithPublicSecurity()
            .RequireRateLimiting(RateLimitPolicies.QuoteResponse);

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

        // QR públicos
        app.MapGet("/api/public/v1/qr/{code}",
                (string code, ScanQrCodeHandler handler, HttpContext ctx, CancellationToken ct) =>
                    ScanQrAsync(code, handler, ctx, ct))
            .AllowAnonymous()
            .WithPublicSecurity()
            .RequireRateLimiting(RateLimitPolicies.QrScan);

        app.MapPost("/api/public/v1/qr/{code}/owner-challenge",
                (string code, OwnerChallengeRequest request,
                 StartOwnerChallengeHandler handler, HttpContext ctx, CancellationToken ct) =>
                    StartChallengeAsync(code, request, handler, ctx, ct))
            .AllowAnonymous()
            .WithPublicSecurity()
            .RequireRateLimiting(RateLimitPolicies.QrChallenge);

        app.MapPost("/api/public/v1/qr/{code}/owner-session",
                (string code, ValidateOwnerOtpRequest request,
                 ValidateOwnerOtpHandler handler, IQrCookieService cookies,
                 HttpContext ctx, CancellationToken ct) =>
                    ValidateOtpAsync(code, request, handler, cookies, ctx, ct))
            .AllowAnonymous()
            .WithPublicSecurity();

        app.MapGet("/api/public/v1/qr/{code}/owner-vehicle",
                (string code, GetOwnerVehicleHandler handler, IQrCookieService cookies,
                 HttpContext ctx, CancellationToken ct) =>
                    GetOwnerVehicleAsync(code, handler, cookies, ctx, ct))
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

    private static async Task<IResult> ScanQrAsync(
        string code, ScanQrCodeHandler handler, HttpContext ctx, CancellationToken ct)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString();
        var ua = ctx.Request.Headers.UserAgent.FirstOrDefault();
        var dto = await handler.HandleAsync(code, ip, ua, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> StartChallengeAsync(
        string code, OwnerChallengeRequest request,
        StartOwnerChallengeHandler handler, HttpContext ctx, CancellationToken ct)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString();
        var result = await handler.HandleAsync(code, request, ip, ct);
        return Results.Accepted(value: result);
    }

    private static async Task<IResult> ValidateOtpAsync(
        string code, ValidateOwnerOtpRequest request,
        ValidateOwnerOtpHandler handler, IQrCookieService cookies,
        HttpContext ctx, CancellationToken ct)
    {
        _ = code; // el challengeId identifica unívocamente el desafío; code es solo para logs
        var session = await handler.HandleAsync(code, request, ct);
        var cookieValue = cookies.Protect(session);
        ctx.Response.Cookies.Append(QrCookieName, cookieValue, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddMinutes(30)
        });
        return Results.Ok(new { SessionIssued = true });
    }

    private static async Task<IResult> GetOwnerVehicleAsync(
        string code, GetOwnerVehicleHandler handler, IQrCookieService cookies,
        HttpContext ctx, CancellationToken ct)
    {
        _ = code;
        var cookieValue = ctx.Request.Cookies[QrCookieName];
        var session = cookies.Unprotect(cookieValue);
        if (session is null)
            return Results.Unauthorized();

        var dto = await handler.HandleAsync(session, ct);
        return Results.Ok(dto);
    }
}
