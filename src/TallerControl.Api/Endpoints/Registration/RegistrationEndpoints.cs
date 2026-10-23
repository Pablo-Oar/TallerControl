using TallerControl.Api.Security;
using TallerControl.Application.Registration;

namespace TallerControl.Api.Endpoints.Registration;

/// <summary>
/// Endpoints públicos de auto-registro (D-22, E10, REQ-01).
/// POST /api/public/v1/register          → crear taller + owner (sin revelar si el email existe)
/// GET  /api/public/v1/verify-email      → confirmar email desde el link del correo
/// POST /api/public/v1/resend-verification → reenviar el enlace (respuesta uniforme)
/// </summary>
internal static class RegistrationEndpoints
{
    public static IEndpointRouteBuilder MapRegistrationEndpoints(
        this IEndpointRouteBuilder app,
        IConfiguration configuration)
    {
        app.MapPost("/api/public/v1/register",
                async (RegisterTenantRequest request, RegisterTenantHandler handler,
                       IConfiguration cfg, CancellationToken ct) =>
                {
                    var frontendBase = cfg["App:FrontendBaseUrl"] ?? "http://localhost:5173";
                    var result = await handler.HandleAsync(request, frontendBase, ct);
                    return Results.Created((string?)null, result);
                })
            .AllowAnonymous()
            .WithPublicSecurity()
            .RequireRateLimiting(RateLimitPolicies.Registration);

        app.MapPost("/api/public/v1/resend-verification",
                async (ResendVerificationRequest request, ResendVerificationHandler handler,
                       IConfiguration cfg, CancellationToken ct) =>
                {
                    var frontendBase = cfg["App:FrontendBaseUrl"] ?? "http://localhost:5173";
                    await handler.HandleAsync(request, frontendBase, ct);
                    return Results.Accepted();
                })
            .AllowAnonymous()
            .WithPublicSecurity()
            .RequireRateLimiting(RateLimitPolicies.Registration);

        app.MapGet("/api/public/v1/verify-email",
                async (string token, VerifyEmailHandler handler, CancellationToken ct) =>
                {
                    var result = await handler.HandleAsync(token, ct);
                    return result.Success
                        ? Results.Ok(result)
                        : Results.BadRequest(result);
                })
            .AllowAnonymous()
            .WithPublicSecurity();

        return app;
    }
}
