using TallerControl.Application.Push;
using TallerControl.Infrastructure.Identity;

namespace TallerControl.Api.Endpoints.Push;

/// <summary>
/// Suscripciones push para la PWA del mecánico (F-51).
/// GET  /api/v1/push/public-key      → clave pública VAPID (404 si no hay push configurado)
/// POST /api/v1/push-subscriptions   → registrar o actualizar suscripción
/// DELETE /api/v1/push-subscriptions → eliminar suscripción
/// Requiere sesión autenticada; el user_id sale del claim tc:uid.
/// </summary>
internal static class PushEndpoints
{
    public static IEndpointRouteBuilder MapPushEndpoints(this IEndpointRouteBuilder app)
    {
        // Clave pública VAPID para que el navegador pueda suscribirse; 404 si el servidor no tiene push configurado.
        app.MapGet("/api/v1/push/public-key",
                (IPushSender sender) =>
                    sender.PublicKey is { } key ? Results.Ok(new { publicKey = key }) : Results.NotFound())
            .RequireAuthorization();

        app.MapPost("/api/v1/push-subscriptions",
                (PushSubscriptionRequest request,
                 SavePushSubscriptionHandler handler,
                 HttpContext ctx, CancellationToken ct) =>
                    SaveAsync(request, handler, ctx, ct))
            .RequireAuthorization();

        app.MapDelete("/api/v1/push-subscriptions",
                (string endpoint,
                 DeletePushSubscriptionHandler handler,
                 HttpContext ctx, CancellationToken ct) =>
                    DeleteAsync(endpoint, handler, ctx, ct))
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> SaveAsync(
        PushSubscriptionRequest request,
        SavePushSubscriptionHandler handler,
        HttpContext ctx, CancellationToken ct)
    {
        var userId = GetUserId(ctx);
        if (userId is null) return Results.Unauthorized();

        await handler.HandleAsync(userId.Value, request, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteAsync(
        string endpoint,
        DeletePushSubscriptionHandler handler,
        HttpContext ctx, CancellationToken ct)
    {
        var userId = GetUserId(ctx);
        if (userId is null) return Results.Unauthorized();

        await handler.HandleAsync(userId.Value, endpoint, ct);
        return Results.NoContent();
    }

    private static Guid? GetUserId(HttpContext ctx)
    {
        var raw = ctx.User.FindFirst(SessionClaims.UserId)?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
