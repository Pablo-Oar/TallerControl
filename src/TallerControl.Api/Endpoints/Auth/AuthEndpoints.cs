using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using TallerControl.Api.Identity;
using TallerControl.Api.Security;
using TallerControl.Application.Auth;
using TallerControl.Application.Users;
using TallerControl.Infrastructure.Identity;

namespace TallerControl.Api.Endpoints.Auth;

/// <summary>
/// Endpoints de autenticación bajo <c>/api/v1/auth</c> (doc 05 §2, T-AUTH-01..06).
/// </summary>
internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(
        this IEndpointRouteBuilder app,
        IConfiguration configuration)
    {
        var frontendBaseUrl = configuration["App:FrontendBaseUrl"] ?? "http://localhost:5173";

        var group = app.MapGroup("/api/v1/auth");

        group.MapGet("/antiforgery-token", GetAntiforgeryToken)
            .AllowAnonymous();

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .AddEndpointFilter<AntiforgeryEndpointFilter>()
            .RequireRateLimiting(RateLimitPolicies.Login);

        group.MapPost("/logout", LogoutAsync)
            .RequireAuthorization()
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        group.MapGet("/me", MeAsync)
            .RequireAuthorization();

        group.MapPost("/forgot-password",
                (ForgotPasswordRequest request, ForgotPasswordHandler handler, CancellationToken ct) =>
                    ForgotPasswordAsync(request, handler, frontendBaseUrl, ct))
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.ForgotPassword);

        group.MapPost("/reset-password",
                async (ResetPasswordRequest request, ResetPasswordHandler handler, CancellationToken ct) =>
                {
                    await handler.HandleAsync(request, ct);
                    return Results.NoContent();
                })
            .AllowAnonymous();

        return app;
    }

    private static IResult GetAntiforgeryToken(HttpContext context, IAntiforgery antiforgery)
    {
        // Genera (si falta) la cookie XSRF-TOKEN y devuelve el token para la cabecera.
        var tokens = antiforgery.GetAndStoreTokens(context);
        return Results.Ok(new { token = tokens.RequestToken });
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        LoginHandler handler,
        HttpContext context,
        CancellationToken ct)
    {
        try
        {
            var result = await handler.HandleAsync(request, ct);
            await SignInAsync(context, result, request.RememberMe);
            return Results.NoContent();
        }
        catch (AuthenticationException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "No autenticado",
                detail: "Email o contraseña incorrectos.");
        }
        catch (AccountLockedException ex)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status423Locked,
                title: "Cuenta bloqueada",
                detail: "La cuenta está bloqueada por intentos fallidos. Volvé a probar en unos minutos.",
                extensions: new Dictionary<string, object?>
                {
                    ["lockoutEnd"] = ex.LockoutEnd,
                });
        }
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        LogoutHandler handler,
        CancellationToken ct)
    {
        var userIdRaw = context.User.FindFirst(SessionClaims.UserId)?.Value;
        if (Guid.TryParse(userIdRaw, out var userId))
        {
            await handler.HandleAsync(userId, ct);
        }

        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.NoContent();
    }

    private static async Task<IResult> MeAsync(
        HttpContext context,
        MeHandler handler,
        CancellationToken ct)
    {
        var userIdRaw = context.User.FindFirst(SessionClaims.UserId)?.Value;
        if (!Guid.TryParse(userIdRaw, out var userId))
        {
            return Results.Unauthorized();
        }

        var me = await handler.HandleAsync(userId, ct);
        return Results.Ok(me);
    }

    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        ForgotPasswordHandler handler,
        string frontendBaseUrl,
        CancellationToken ct)
    {
        await handler.HandleAsync(request, frontendBaseUrl, ct);
        // Siempre 202: no revelar si el email existe (T-AUTH-07).
        return Results.Accepted();
    }

    private static async Task SignInAsync(HttpContext context, LoginResult result, bool rememberMe)
    {
        var claims = new List<Claim>
        {
            new(SessionClaims.UserId, result.UserId.ToString()),
            new(SessionClaims.TenantId, result.TenantId.ToString()),
            new(SessionClaims.Role, result.Role.ToString()),
            new(SessionClaims.SecurityStamp, result.SecurityStamp),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var properties = new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            AllowRefresh = true,
        };

        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
    }
}
