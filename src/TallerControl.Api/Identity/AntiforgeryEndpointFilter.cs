using Microsoft.AspNetCore.Antiforgery;

namespace TallerControl.Api.Identity;

/// <summary>
/// Valida el token antiforgery (cookie <c>XSRF-TOKEN</c> + cabecera <c>X-CSRF-TOKEN</c>) en los
/// endpoints que modifican estado. Si falla devuelve 400 con <c>application/problem+json</c>
/// (doc 05 §1, T-AUTH-04). Los Minimal APIs con contenido JSON no exigen antiforgery por defecto;
/// este filtro lo fuerza para los endpoints que lo requieren.
/// </summary>
internal sealed class AntiforgeryEndpointFilter : IEndpointFilter
{
    private readonly IAntiforgery _antiforgery;

    public AntiforgeryEndpointFilter(IAntiforgery antiforgery)
    {
        _antiforgery = antiforgery;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            await _antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Solicitud inválida",
                detail: "Falta o es inválida la cabecera antiforgery.");
        }

        return await next(context);
    }
}
