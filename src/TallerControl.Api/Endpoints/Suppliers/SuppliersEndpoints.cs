using TallerControl.Api.Identity;
using TallerControl.Application.Suppliers;
using TallerControl.Domain.Users;

namespace TallerControl.Api.Endpoints.Suppliers;

/// <summary>
/// Endpoints de proveedores (T-SUP-01..03, REQ-E11).
/// GET    /api/v1/suppliers                              → suppliers.read  (O, A)
/// POST   /api/v1/suppliers                              → suppliers.write (O, A)
/// GET    /api/v1/suppliers/{id}                         → suppliers.read  (O, A)
/// PUT    /api/v1/suppliers/{id}                         → suppliers.write (O, A)
/// DELETE /api/v1/suppliers/{id}                         → suppliers.write (O, A)
/// POST   /api/v1/suppliers/{id}/restore                 → suppliers.write (O, A)
/// </summary>
internal static class SuppliersEndpoints
{
    public static IEndpointRouteBuilder MapSuppliersEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/suppliers",
                (SearchSuppliersHandler handler, CancellationToken ct,
                 string? q = null, bool deleted = false, int page = 1, int pageSize = 25) =>
                    handler.HandleAsync(q, deleted, page, pageSize, ct))
            .RequirePermission(Permission.SuppliersRead);

        app.MapPost("/api/v1/suppliers",
                (CreateSupplierRequest request, CreateSupplierHandler handler, CancellationToken ct) =>
                    CreateAsync(request, handler, ct))
            .RequirePermission(Permission.SuppliersWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/v1/suppliers/{id:guid}",
                (Guid id, GetSupplierHandler handler, CancellationToken ct) =>
                    GetAsync(id, handler, ct))
            .RequirePermission(Permission.SuppliersRead);

        app.MapPut("/api/v1/suppliers/{id:guid}",
                (Guid id, UpdateSupplierRequest request, UpdateSupplierHandler handler,
                 HttpRequest httpRequest, CancellationToken ct) =>
                    UpdateAsync(id, request, handler, httpRequest, ct))
            .RequirePermission(Permission.SuppliersWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapDelete("/api/v1/suppliers/{id:guid}",
                (Guid id, [Microsoft.AspNetCore.Mvc.FromBody] DeleteSupplierRequest request,
                 DeleteSupplierHandler handler, CancellationToken ct) =>
                    DeleteAsync(id, request.Reason, handler, ct))
            .RequirePermission(Permission.SuppliersWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/suppliers/{id:guid}/restore",
                (Guid id, RestoreSupplierHandler handler, CancellationToken ct) =>
                    RestoreAsync(id, handler, ct))
            .RequirePermission(Permission.SuppliersWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateSupplierRequest request, CreateSupplierHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(request, ct);
        return Results.Created($"/api/v1/suppliers/{dto.Id}", dto);
    }

    private static async Task<IResult> GetAsync(
        Guid id, GetSupplierHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(id, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, UpdateSupplierRequest request, UpdateSupplierHandler handler,
        HttpRequest httpRequest, CancellationToken ct)
    {
        var ifMatch = httpRequest.Headers.IfMatch.FirstOrDefault();
        if (!TryParseETag(ifMatch, out var expectedVersion))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Cabecera requerida",
                detail: "Se requiere la cabecera If-Match con la versión del recurso.");
        }

        var requestWithVersion = request with { Version = expectedVersion };
        var dto = await handler.HandleAsync(id, requestWithVersion, ct);
        return Results.Ok(dto);
    }

    private static bool TryParseETag(string? raw, out uint version)
    {
        version = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        return uint.TryParse(raw.Trim('"'), out version);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id, string? reason, DeleteSupplierHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(id, reason, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> RestoreAsync(
        Guid id, RestoreSupplierHandler handler, CancellationToken ct)
    {
        var dto = await handler.HandleAsync(id, ct);
        return Results.Ok(dto);
    }
}
