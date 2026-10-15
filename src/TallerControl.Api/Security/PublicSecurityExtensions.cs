using TallerControl.Application.Common;

namespace TallerControl.Api.Security;

/// <summary>
/// Filtro de seguridad para endpoints públicos anónimos:
/// añade Cache-Control: no-store, X-Robots-Tag: noindex, Referrer-Policy: no-referrer
/// y convierte NotFoundException en 404 para que los headers lleguen igualmente.
/// </summary>
internal static class PublicSecurityExtensions
{
    public static RouteHandlerBuilder WithPublicSecurity(this RouteHandlerBuilder builder) =>
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
}
