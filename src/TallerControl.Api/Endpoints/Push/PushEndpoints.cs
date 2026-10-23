using TallerControl.Api.Identity;
using TallerControl.Application.Push;
using TallerControl.Infrastructure.Identity;

namespace TallerControl.Api.Endpoints.Push;

/// <summary>
/// Suscripciones push para la PWA del mecánico (F-51).
/// GET  /api/v1/push/public-key      → clave pública VAPID (404 si no hay push configurado)
/// POST /api/v1/push/test            → aviso de prueba al propio usuario
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

        // Aviso de prueba al propio usuario: devuelve qué respondió el servicio push, para diagnosticar.
        app.MapPost("/api/v1/push/test",
                async (IPushSender sender, HttpContext ctx, CancellationToken ct) =>
                {
                    var userId = GetUserId(ctx);
                    if (userId is null) return Results.Unauthorized();
                    if (sender.PublicKey is null)
                        return Results.Ok(new { configured = false, subscriptions = 0, delivered = 0, failures = Array.Empty<string>(), statuses = Array.Empty<int>() });

                    var result = await sender.SendToUserAsync(
                        userId.Value, new PushMessage("Aviso de prueba", "Si ves esto, los avisos funcionan.", "/m"), ct);
                    return Results.Ok(new
                    {
                        configured = true,
                        subscriptions = result.Subscriptions,
                        delivered = result.Delivered,
                        failures = result.Failures,
                        statuses = result.Statuses ?? [],
                    });
                })
            .RequireAuthorization()
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

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
