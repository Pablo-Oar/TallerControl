using TallerControl.Api.Identity;
using TallerControl.Application.Auth;
using TallerControl.Application.Notifications;
using TallerControl.Domain.Users;
using TallerControl.Infrastructure.Identity;

namespace TallerControl.Api.Endpoints.Notifications;

/// <summary>
/// Avisos internos del panel (la campanita).
/// GET  /api/v1/notifications              → notifications.read (O, A)
/// POST /api/v1/notifications/{id}/read    → notifications.read (O, A)
/// POST /api/v1/notifications/read-all     → notifications.read (O, A)
/// </summary>
internal static class NotificationsEndpoints
{
    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/notifications",
                async (NotificationsHandler handler, IAuthRepository auth, HttpContext ctx, CancellationToken ct) =>
                    Results.Ok(await handler.ListAsync(await MembershipIdAsync(auth, ctx, ct), ct)))
            .RequirePermission(Permission.NotificationsRead);

        app.MapPost("/api/v1/notifications/{id:guid}/read",
                async (Guid id, NotificationsHandler handler, IAuthRepository auth, HttpContext ctx, CancellationToken ct) =>
                {
                    await handler.MarkReadAsync(id, await MembershipIdAsync(auth, ctx, ct), ct);
                    return Results.NoContent();
                })
            .RequirePermission(Permission.NotificationsRead)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/notifications/read-all",
                async (NotificationsHandler handler, IAuthRepository auth, HttpContext ctx, CancellationToken ct) =>
                {
                    await handler.MarkAllReadAsync(await MembershipIdAsync(auth, ctx, ct), ct);
                    return Results.NoContent();
                })
            .RequirePermission(Permission.NotificationsRead)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }

    private static async Task<Guid> MembershipIdAsync(IAuthRepository auth, HttpContext ctx, CancellationToken ct)
    {
        var raw = ctx.User.FindFirst(SessionClaims.UserId)?.Value;
        if (!Guid.TryParse(raw, out var userId))
            throw new UnauthorizedAccessException();
        var membership = await auth.FindActiveMembershipAsync(userId, ct)
            ?? throw new UnauthorizedAccessException();
        return membership.Membership.Id;
    }
}
