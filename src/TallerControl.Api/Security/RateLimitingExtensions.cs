using System.Threading.RateLimiting;
using TallerControl.Infrastructure.Identity;

namespace TallerControl.Api.Security;

/// <summary>
/// Rate limiting por endpoint (doc-07 §6). Un solo nodo usa limitadores en memoria.
/// - GlobalLimiter: 600 req/min por userId (autenticado) o por IP (anónimo). Aplica a todo /api/*.
///   Las rutas /health/* quedan exentas explícitamente.
/// - Políticas de endpoint (más estrictas, aplican además del global):
///   ver <see cref="RateLimitPolicies"/>; se usan con <c>.RequireRateLimiting("nombre")</c>.
/// </summary>
internal static class RateLimitingExtensions
{
    public static IServiceCollection AddTallerControlRateLimiting(
        this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Global: 600/min por userId o IP — capa base para toda la API.
            // Health checks y statics quedan exentos.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/health"))
                    return RateLimitPartition.GetNoLimiter("health");

                var userId = ctx.User.FindFirst(SessionClaims.UserId)?.Value;
                var key = userId is null ? $"ip:{GetIp(ctx)}" : $"u:{userId}";
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: key,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 600,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    });
            });

            // POST /auth/login — 10 req/min por IP
            options.AddPolicy(RateLimitPolicies.Login, ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetIp(ctx),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            // POST /auth/forgot-password — 10 req/h por IP
            // (límite por email 3/h se aplica en Application; esto es la capa de red)
            options.AddPolicy(RateLimitPolicies.ForgotPassword, ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetIp(ctx),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0,
                    }));

            // GET /api/public/v1/qr/{code} — 30 req/min por IP
            options.AddPolicy(RateLimitPolicies.QrScan, ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetIp(ctx),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            // POST .../qr/{code}/owner-challenge — 5 req/15 min por IP
            options.AddPolicy(RateLimitPolicies.QrChallenge, ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetIp(ctx),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(15),
                        QueueLimit = 0,
                    }));

            // GET /api/public/v1/quotes/{token} — 60 req/min por IP
            options.AddPolicy(RateLimitPolicies.PublicQuote, ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetIp(ctx),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            // POST /api/public/v1/register — 5 req/15 min por IP (anti-spam de cuentas)
            options.AddPolicy(RateLimitPolicies.Registration, ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: $"reg:{GetIp(ctx)}",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(15),
                        QueueLimit = 0,
                    }));

            // POST .../quotes/{token}/responses — 10 req/h por token
            options.AddPolicy(RateLimitPolicies.QuoteResponse, ctx =>
            {
                var token = ctx.GetRouteValue("token")?.ToString() ?? "anon";
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: $"qr:{token}",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0,
                    });
            });

            // POST .../qr/{code}/found-keys — 2 req/10 min por IP (anti-spam de alertas)
            options.AddPolicy(RateLimitPolicies.FoundKeys, ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: $"fk:{GetIp(ctx)}",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 2,
                        Window = TimeSpan.FromMinutes(10),
                        QueueLimit = 0,
                    }));
        });

        return services;
    }

    private static string GetIp(HttpContext ctx) =>
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anon";
}

/// <summary>Nombres de las políticas de rate limiting (endpoints más estrictos que el global).</summary>
internal static class RateLimitPolicies
{
    public const string Login = "login";
    public const string ForgotPassword = "forgot-password";
    public const string Registration = "registration";
    public const string QrScan = "qr-scan";
    public const string QrChallenge = "qr-challenge";
    public const string PublicQuote = "public-quote";
    public const string QuoteResponse = "quote-response";
    public const string FoundKeys = "found-keys";
}
