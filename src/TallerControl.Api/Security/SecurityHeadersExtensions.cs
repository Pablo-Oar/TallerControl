namespace TallerControl.Api.Security;

/// <summary>
/// Middleware de cabeceras de seguridad HTTP (doc-07 §11).
/// </summary>
internal static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        app.Use(async (ctx, next) =>
        {
            var headers = ctx.Response.Headers;

            // HSTS — solo significativo en HTTPS; en HTTP los navegadores lo ignoran.
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

            // Impide inyección de tipos MIME (sniffing).
            headers["X-Content-Type-Options"] = "nosniff";

            // No se puede incrustar en iframe externo (clickjacking).
            headers["X-Frame-Options"] = "DENY";

            // Evita enviar el referer fuera del origen.
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // CSP: permite imágenes de R2 (CDN configurado en App:R2PublicDomain).
            // data: para favicon/logo inline en la SPA; frame-ancestors 'none' refuerza X-Frame-Options.
            var r2Domain = ctx.RequestServices
                .GetRequiredService<IConfiguration>()["App:R2PublicDomain"];
            var imgSrc = string.IsNullOrWhiteSpace(r2Domain)
                ? "'self' data:"
                : $"'self' data: https://{r2Domain}";

            headers["Content-Security-Policy"] =
                $"default-src 'self'; " +
                $"script-src 'self'; " +
                $"style-src 'self' 'unsafe-inline'; " +
                $"img-src {imgSrc}; " +
                $"font-src 'self'; " +
                $"connect-src 'self'; " +
                $"frame-ancestors 'none'";

            await next();
        });

        return app;
    }
}
