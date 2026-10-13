using TallerControl.Api.Identity;
using TallerControl.Application.Users;
using TallerControl.Domain.Users;

namespace TallerControl.Api.Endpoints.Users;

/// <summary>
/// Endpoints de gestión de miembros del taller (doc 05 §2, REQ-03).
/// GET  /api/v1/users                    → Owner (users.manage)   → T-PERM-01
/// PATCH /api/v1/users/{membershipId}    → Owner (users.manage)   → T-USR-05..07
/// </summary>
internal static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/users",
                (ListMembersHandler handler, CancellationToken ct) => handler.HandleAsync(ct))
            .RequirePermission(Permission.UsersManage);

        app.MapPatch("/api/v1/users/{membershipId:guid}",
                (Guid membershipId,
                    UpdateMembershipRequest request,
                    UpdateMembershipHandler handler,
                    HttpRequest httpRequest,
                    CancellationToken ct) =>
                    UpdateAsync(membershipId, request, handler, httpRequest, ct))
            .RequirePermission(Permission.UsersManage)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }

    private static async Task<IResult> UpdateAsync(
        Guid membershipId,
        UpdateMembershipRequest request,
        UpdateMembershipHandler handler,
        HttpRequest httpRequest,
        CancellationToken ct)
    {
        var ifMatch = httpRequest.Headers.IfMatch.FirstOrDefault();
        if (!TryParseETag(ifMatch, out var expectedVersion))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Cabecera requerida",
                detail: "Se requiere la cabecera If-Match con la versión del recurso.");
        }

        var dto = await handler.HandleAsync(membershipId, request, expectedVersion, ct);
        return Results.Ok(dto);
    }

    /// <summary>Parsea ETag con o sin comillas: <c>"3"</c> o <c>3</c>.</summary>
    private static bool TryParseETag(string? raw, out uint version)
    {
        version = 0;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim('"');
        return uint.TryParse(trimmed, out version);
    }
}
