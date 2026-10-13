using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TallerControl.Application.Common;
using TallerControl.Infrastructure.Identity;

namespace TallerControl.Api.Identity;

/// <summary>
/// Configura la autenticación por cookie <c>tc_session</c>, la antiforgery con cabecera
/// <c>X-CSRF-TOKEN</c>, y las implementaciones basadas en sesión de <see cref="ITenantContext"/>,
/// <see cref="IBranchContext"/> y <see cref="IActorContext"/> (doc 05 §1, doc 07 §1-§2).
/// Debe llamarse después de <c>AddInfrastructure</c> para reemplazar los contextos por defecto.
/// </summary>
public static class AuthenticationSetup
{
    public const string SessionCookieName = "tc_session";
    public const string AntiforgeryHeaderName = "X-CSRF-TOKEN";
    public const string AntiforgeryCookieName = ".tc.antiforgery";

    public static IServiceCollection AddTallerControlAuthentication(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        services.Replace(ServiceDescriptor.Scoped<ITenantContext, SessionTenantContext>());
        services.Replace(ServiceDescriptor.Scoped<IBranchContext, SessionTenantContext>());
        services.Replace(ServiceDescriptor.Scoped<IActorContext, SessionActorContext>());

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = SessionCookieName;
                options.Cookie.HttpOnly = true;
                // SameAsRequest emite Secure solo cuando la request llegó por HTTPS. En producción
                // el reverse proxy fuerza HTTPS + HSTS; en pruebas sobre TestServer la cookie fluye.
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;

                // Para una API no redirigimos: devolvemos 401/403 en respuestas automáticas.
                options.Events.OnRedirectToLogin = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };

                options.Events.OnValidatePrincipal = ValidateSecurityStampAsync;
            });

        services.AddAuthorization();

        services.AddAntiforgery(options =>
        {
            options.HeaderName = AntiforgeryHeaderName;
            // La cookie guarda el "cookie token": sigue siendo HttpOnly. El cliente obtiene el
            // "request token" por GET /api/v1/auth/antiforgery-token y lo envía en X-CSRF-TOKEN.
            options.Cookie.Name = AntiforgeryCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Lax;
        });

        return services;
    }

    private static async Task ValidateSecurityStampAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        if (principal is null)
        {
            context.RejectPrincipal();
            return;
        }

        var userIdRaw = principal.FindFirst(SessionClaims.UserId)?.Value;
        var stamp = principal.FindFirst(SessionClaims.SecurityStamp)?.Value;

        if (!Guid.TryParse(userIdRaw, out var userId) || string.IsNullOrEmpty(stamp))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        var validator = context.HttpContext.RequestServices.GetRequiredService<SecurityStampValidator>();
        if (!await validator.IsValidAsync(userId, stamp, context.HttpContext.RequestAborted))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
