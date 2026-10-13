using TallerControl.Api.Identity;
using TallerControl.Application.Settings;
using TallerControl.Domain.Users;

namespace TallerControl.Api.Endpoints.Settings;

/// <summary>
/// Endpoints de configuración del taller (doc 05 §2.2, REQ-04). Todos requieren
/// <c>settings.manage</c>: la matriz de doc 07 §3 no da acceso de lectura a otros roles.
/// GET  /api/v1/settings                   → T-SET-01
/// PUT  /api/v1/settings                   → T-SET-02
/// POST /api/v1/settings/logo/upload-url   → T-SET-03
/// POST /api/v1/settings/logo/confirm
/// </summary>
internal static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/settings")
            .RequireAuthorization(PermissionRequirement.PermissionPolicy(Permission.SettingsManage));

        group.MapGet("/", (GetSettingsHandler handler, CancellationToken ct) => handler.HandleAsync(ct));

        group.MapPut("/",
                (UpdateSettingsRequest request, UpdateSettingsHandler handler, HttpRequest httpRequest, CancellationToken ct) =>
                    UpdateAsync(request, handler, httpRequest, ct))
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        group.MapPost("/logo/upload-url",
                (LogoUploadUrlRequest request, GetLogoUploadUrlHandler handler, CancellationToken ct) =>
                    handler.HandleAsync(request, ct))
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        group.MapPost("/logo/confirm",
                (ConfirmLogoHandler handler, CancellationToken ct) => handler.HandleAsync(ct))
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }

    private static async Task<IResult> UpdateAsync(
        UpdateSettingsRequest request,
        UpdateSettingsHandler handler,
        HttpRequest httpRequest,
        CancellationToken ct)
    {
        var ifMatch = httpRequest.Headers.IfMatch.FirstOrDefault();
        if (ifMatch is null
            || !SettingsVersion.TryParse(ifMatch.Trim('"'), out var tenantVersion, out var settingsVersion))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Cabecera requerida",
                detail: "Se requiere la cabecera If-Match con la versión del recurso.");
        }

        var dto = await handler.HandleAsync(request, tenantVersion, settingsVersion, ct);
        return Results.Ok(dto);
    }
}
