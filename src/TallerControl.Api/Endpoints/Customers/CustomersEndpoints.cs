using TallerControl.Api.Identity;
using TallerControl.Application.Customers;
using TallerControl.Domain.Users;

namespace TallerControl.Api.Endpoints.Customers;

/// <summary>
/// Endpoints de clientes (doc 05 §2.3, REQ-05).
/// GET    /api/v1/customers                     → customers.read   (O, A)
/// POST   /api/v1/customers                     → customers.write  (O, A)
/// GET    /api/v1/customers/{id}                → customers.read   (O, A)   T-ISO-01
/// PUT    /api/v1/customers/{id}                → customers.write  (O, A)
/// DELETE /api/v1/customers/{id}                → customers.delete (O)
/// POST   /api/v1/customers/{id}/restore        → customers.delete (O)
/// </summary>
internal static class CustomersEndpoints
{
    public static IEndpointRouteBuilder MapCustomersEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/customers",
                (SearchCustomersHandler handler, CancellationToken ct,
                 string? q = null, int page = 1, int pageSize = 25) =>
                    handler.HandleAsync(q, page, pageSize, ct))
            .RequirePermission(Permission.CustomersRead);

        app.MapPost("/api/v1/customers",
                (CustomerUpsertRequest request, CreateCustomerHandler handler, CancellationToken ct) =>
                    CreateAsync(request, handler, ct))
            .RequirePermission(Permission.CustomersWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/v1/customers/{id:guid}",
                (Guid id, GetCustomerHandler handler, CancellationToken ct) =>
                    handler.HandleAsync(id, ct))
            .RequirePermission(Permission.CustomersRead);

        app.MapPut("/api/v1/customers/{id:guid}",
                (Guid id, CustomerUpsertRequest request, UpdateCustomerHandler handler,
                 HttpRequest httpRequest, CancellationToken ct) =>
                    UpdateAsync(id, request, handler, httpRequest, ct))
            .RequirePermission(Permission.CustomersWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapDelete("/api/v1/customers/{id:guid}",
                (Guid id, [Microsoft.AspNetCore.Mvc.FromBody] DeleteCustomerRequest request,
                 DeleteCustomerHandler handler, CancellationToken ct) =>
                    DeleteAsync(id, request.Reason, handler, ct))
            .RequirePermission(Permission.CustomersDelete)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/customers/{id:guid}/restore",
                (Guid id, RestoreCustomerHandler handler, CancellationToken ct) =>
                    RestoreAsync(id, handler, ct))
            .RequirePermission(Permission.CustomersDelete)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CustomerUpsertRequest request,
        CreateCustomerHandler handler,
        CancellationToken ct)
    {
        var dto = await handler.HandleAsync(request, ct);
        return Results.Created($"/api/v1/customers/{dto.Id}", dto);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        CustomerUpsertRequest request,
        UpdateCustomerHandler handler,
        HttpRequest httpRequest,
        CancellationToken ct)
    {
        if (!TryParseETag(httpRequest.Headers.IfMatch.FirstOrDefault(), out var version))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Cabecera requerida",
                detail: "Se requiere la cabecera If-Match con la versión del recurso.");
        }

        var dto = await handler.HandleAsync(id, version, request, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        string reason,
        DeleteCustomerHandler handler,
        CancellationToken ct)
    {
        await handler.HandleAsync(id, reason, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> RestoreAsync(
        Guid id,
        RestoreCustomerHandler handler,
        CancellationToken ct)
    {
        var dto = await handler.HandleAsync(id, ct);
        return Results.Ok(dto);
    }

    private static bool TryParseETag(string? raw, out uint version)
    {
        version = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        return uint.TryParse(raw.Trim('"'), out version);
    }
}
