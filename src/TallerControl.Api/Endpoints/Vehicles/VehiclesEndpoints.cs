using Microsoft.AspNetCore.Authorization;
using TallerControl.Api.Identity;
using TallerControl.Application.Auth;
using TallerControl.Application.Vehicles;
using TallerControl.Domain.Users;
using TallerControl.Infrastructure.Identity;

namespace TallerControl.Api.Endpoints.Vehicles;

/// <summary>
/// Endpoints de vehículos (doc 05 §2.3, REQ-06).
/// GET    /api/v1/vehicles                       → vehicles.read  (O, A, M)
/// POST   /api/v1/vehicles                       → vehicles.write (O, A, M)
/// GET    /api/v1/vehicles/{id}                  → vehicles.read  (O, A, M)
/// PUT    /api/v1/vehicles/{id}                  → vehicles.write (O, A)
/// DELETE /api/v1/vehicles/{id}                  → vehicles.write (O) [plan dice O]
/// GET    /api/v1/vehicles/{id}/history          → vehicles.read  (O, A, M)
/// </summary>
internal static class VehiclesEndpoints
{
    public static IEndpointRouteBuilder MapVehiclesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/vehicles",
                (SearchVehiclesHandler handler, CancellationToken ct,
                 string? q = null, int page = 1, int pageSize = 25) =>
                    handler.HandleAsync(q, page, pageSize, ct))
            .RequirePermission(Permission.VehiclesRead);

        app.MapPost("/api/v1/vehicles",
                (VehicleUpsertRequest request, CreateVehicleHandler handler, CancellationToken ct) =>
                    CreateAsync(request, handler, ct))
            .RequirePermission(Permission.VehiclesWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/v1/vehicles/{id:guid}",
                (Guid id, GetVehicleHandler handler, CancellationToken ct) =>
                    handler.HandleAsync(id, ct))
            .RequirePermission(Permission.VehiclesRead);

        app.MapPut("/api/v1/vehicles/{id:guid}",
                (Guid id, VehicleUpsertRequest request, UpdateVehicleHandler handler,
                 HttpRequest httpRequest, IAuthorizationService authz,
                 HttpContext httpContext, CancellationToken ct) =>
                    UpdateAsync(id, request, handler, httpRequest, authz, httpContext, ct))
            .RequirePermission(Permission.VehiclesWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapDelete("/api/v1/vehicles/{id:guid}",
                (Guid id, [Microsoft.AspNetCore.Mvc.FromBody] DeleteVehicleRequest request,
                 DeleteVehicleHandler handler, CancellationToken ct) =>
                    DeleteAsync(id, request.Reason, handler, ct))
            .RequirePermission(Permission.VehiclesWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/v1/vehicles/{id:guid}/history",
                async (Guid id, GetVehicleHistoryHandler handler, IAuthRepository auth, HttpContext ctx,
                    CancellationToken ct, int page = 1, int pageSize = 25) =>
                {
                    // El total solo viaja si el rol puede ver precios (el mecánico no).
                    var includePrices = false;
                    if (Guid.TryParse(ctx.User.FindFirst(SessionClaims.UserId)?.Value, out var userId))
                    {
                        var membership = await auth.FindActiveMembershipAsync(userId, ct);
                        includePrices = membership?.Membership.EffectivePermissions().Contains(Permission.PricesView) ?? false;
                    }

                    return Results.Ok(await handler.HandleAsync(id, includePrices, page, pageSize, ct));
                })
            .RequirePermission(Permission.VehiclesRead);

        return app;
    }

    private static async Task<IResult> CreateAsync(
        VehicleUpsertRequest request,
        CreateVehicleHandler handler,
        CancellationToken ct)
    {
        var dto = await handler.HandleAsync(request, ct);
        return Results.Created($"/api/v1/vehicles/{dto.Id}", dto);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        VehicleUpsertRequest request,
        UpdateVehicleHandler handler,
        HttpRequest httpRequest,
        IAuthorizationService authz,
        HttpContext httpContext,
        CancellationToken ct)
    {
        if (!TryParseETag(httpRequest.Headers.IfMatch.FirstOrDefault(), out var version))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Cabecera requerida",
                detail: "Se requiere la cabecera If-Match con la versión del recurso.");
        }

        // vehicles.odometer.correct controla si se puede bajar el km (T-VEH-06..07)
        var canCorrect = await authz.AuthorizeAsync(
            httpContext.User,
            null,
            PermissionRequirement.PermissionPolicy(Permission.VehiclesOdometerCorrect));

        var dto = await handler.HandleAsync(id, version, request, canCorrect.Succeeded, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        string reason,
        DeleteVehicleHandler handler,
        CancellationToken ct)
    {
        await handler.HandleAsync(id, reason, ct);
        return Results.NoContent();
    }

    private static bool TryParseETag(string? raw, out uint version)
    {
        version = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        return uint.TryParse(raw.Trim('"'), out version);
    }
}
