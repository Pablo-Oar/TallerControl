using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using TallerControl.Api.Identity;
using TallerControl.Application.Users;
using TallerControl.Domain.Users;
using TallerControl.Infrastructure.Identity;

namespace TallerControl.Api.Endpoints.Users;

/// <summary>
/// Endpoints de invitaciones (doc 05 §2, REQ-02, T-USR-01..04).
/// POST /api/v1/invitations              → Owner con users.manage
/// GET  /api/v1/invitations              → users.manage (pendientes)
/// POST /api/v1/invitations/{id}/resend  → users.manage (token y email nuevos)
/// DELETE /api/v1/invitations/{id}       → users.manage (cancela)
/// POST /api/public/v1/invitations/{token}/accept → Anónimo (superficie pública, doc 07 §2)
/// </summary>
internal static class InvitationEndpoints
{
    public static IEndpointRouteBuilder MapInvitationEndpoints(
        this IEndpointRouteBuilder app,
        IConfiguration configuration)
    {
        var frontendBaseUrl = configuration["App:FrontendBaseUrl"] ?? "http://localhost:5173";

        app.MapPost("/api/v1/invitations", (CreateInvitationRequest request,
                CreateInvitationHandler handler,
                CancellationToken ct) =>
                CreateAsync(request, handler, frontendBaseUrl, ct))
            .RequirePermission(Permission.UsersManage)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/v1/invitations",
                (ListInvitationsHandler handler, CancellationToken ct) => handler.HandleAsync(ct))
            .RequirePermission(Permission.UsersManage);

        app.MapPost("/api/v1/invitations/{id:guid}/resend",
                async (Guid id, ResendInvitationHandler handler, CancellationToken ct) =>
                    Results.Ok(await handler.HandleAsync(id, frontendBaseUrl, ct)))
            .RequirePermission(Permission.UsersManage)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapDelete("/api/v1/invitations/{id:guid}",
                async (Guid id, CancelInvitationHandler handler, CancellationToken ct) =>
                {
                    await handler.HandleAsync(id, ct);
                    return Results.NoContent();
                })
            .RequirePermission(Permission.UsersManage)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/public/v1/invitations/{token}/accept",
                (string token, AcceptInvitationRequest request,
                AcceptInvitationHandler handler,
                HttpContext context,
                CancellationToken ct) =>
                AcceptAsync(token, request, handler, context, ct))
            .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateInvitationRequest request,
        CreateInvitationHandler handler,
        string frontendBaseUrl,
        CancellationToken ct)
    {
        var dto = await handler.HandleAsync(request, frontendBaseUrl, ct);
        return Results.Created($"/api/v1/invitations/{dto.Id}", dto);
    }

    private static async Task<IResult> AcceptAsync(
        string token,
        AcceptInvitationRequest request,
        AcceptInvitationHandler handler,
        HttpContext context,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(token, request, ct);

        var claims = new List<Claim>
        {
            new(SessionClaims.UserId, result.UserId.ToString()),
            new(SessionClaims.TenantId, result.TenantId.ToString()),
            new(SessionClaims.Role, result.Role.ToString()),
            new(SessionClaims.SecurityStamp, result.SecurityStamp),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });

        return Results.NoContent();
    }
}
