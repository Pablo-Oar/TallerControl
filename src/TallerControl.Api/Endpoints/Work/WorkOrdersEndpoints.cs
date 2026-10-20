using TallerControl.Api.Identity;
using TallerControl.Application.Auth;
using TallerControl.Application.Common;
using TallerControl.Application.Work;
using TallerControl.Domain.Users;
using TallerControl.Domain.Work;
using TallerControl.Infrastructure.Identity;


namespace TallerControl.Api.Endpoints.Work;

/// <summary>
/// Endpoints de órdenes de trabajo (doc 05 §2.7, REQ-12, REQ-14).
/// GET    /api/v1/work-orders                           → workorders.read     (O, A, M*)
/// POST   /api/v1/work-orders                           → workorders.write    (O, A)
/// GET    /api/v1/work-orders/{id}                      → workorders.read     (O, A, M*)
/// PUT    /api/v1/work-orders/{id}                      → workorders.write    (O, A)
/// POST   /api/v1/work-orders/{id}/status               → workorders.status   (O, A, M*)
/// GET    /api/v1/work-orders/mechanics                 → workorders.write    (O, A) mecánicos asignables
/// POST   /api/v1/work-orders/{id}/assign               → workorders.write / workorders.assign_self
/// PATCH  /api/v1/work-orders/{id}/mechanic-notes       → workorders.status   (M)
/// GET    /api/v1/work-orders/{id}/history              → workorders.read     (O, A)
/// GET    /api/v1/work-orders/{id}/pdf                  → workorders.read     (O, A)
/// GET    /api/v1/work-orders/{id}/public-link          → workorders.read     (O, A)
/// * El mecánico solo ve las suyas o sin asignar; la lógica está en el handler.
/// </summary>
internal static class WorkOrdersEndpoints
{
    public static IEndpointRouteBuilder MapWorkOrdersEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/work-orders",
                (SearchWorkOrdersHandler handler, IAuthRepository auth,
                 HttpContext ctx, CancellationToken ct,
                 string? status = null, Guid? mechanicId = null, Guid? vehicleId = null,
                 bool? withBalance = null, DateTimeOffset? from = null, DateTimeOffset? to = null,
                 string? assignment = null, string? sort = null, string? dir = null, int page = 1, int pageSize = 25) =>
                {
                    WorkOrderStatus? parsedStatus = status is not null
                        && EnumQuery.TryParse<WorkOrderStatus>(status, out var s)
                        ? s : null;
                    var parsedAssignment = assignment is not null
                        && EnumQuery.TryParse<AssignmentFilter>(assignment, out var a)
                        ? a : AssignmentFilter.Any;
                    return SearchAsync(handler, auth, ctx, parsedStatus, mechanicId, vehicleId, withBalance, from, to, parsedAssignment, SortSpec.Parse(sort, dir), page, pageSize, ct);
                })
            .RequirePermission(Permission.WorkOrdersRead);

        app.MapPost("/api/v1/work-orders",
                (CreateWorkOrderRequest request, CreateWorkOrderHandler handler, CancellationToken ct) =>
                    CreateAsync(request, handler, ct))
            .RequirePermission(Permission.WorkOrdersWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/v1/work-orders/{id:guid}",
                (Guid id, GetWorkOrderHandler handler, IAuthRepository auth,
                 HttpContext ctx, CancellationToken ct) =>
                    GetAsync(id, handler, auth, ctx, ct))
            .RequirePermission(Permission.WorkOrdersRead);

        app.MapPut("/api/v1/work-orders/{id:guid}",
                (Guid id, UpdateWorkOrderRequest request, UpdateWorkOrderHandler handler,
                 HttpRequest httpRequest, CancellationToken ct) =>
                    UpdateAsync(id, request, handler, httpRequest, ct))
            .RequirePermission(Permission.WorkOrdersWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/work-orders/{id:guid}/status",
                (Guid id, ChangeStatusRequest request, ChangeWorkOrderStatusHandler handler,
                 IAuthRepository auth, HttpContext ctx, CancellationToken ct) =>
                    ChangeStatusAsync(id, request, handler, auth, ctx, ct))
            .RequirePermission(Permission.WorkOrdersStatus)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/v1/work-orders/mechanics",
                (ListAssignableMechanicsHandler handler, CancellationToken ct) => handler.HandleAsync(ct))
            .RequirePermission(Permission.WorkOrdersWrite);

        app.MapPost("/api/v1/work-orders/{id:guid}/assign",
                (Guid id, AssignMechanicRequest request, AssignMechanicHandler handler,
                 IAuthRepository auth, HttpContext ctx, CancellationToken ct) =>
                    AssignAsync(id, request, handler, auth, ctx, ct))
            .RequireAnyPermission(Permission.WorkOrdersWrite, Permission.WorkOrdersAssignSelf)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPatch("/api/v1/work-orders/{id:guid}/mechanic-notes",
                (Guid id, UpdateMechanicNotesRequest request, UpdateMechanicNotesHandler handler,
                 IAuthRepository auth, HttpContext ctx, CancellationToken ct) =>
                    UpdateMechanicNotesAsync(id, request, handler, auth, ctx, ct))
            .RequirePermission(Permission.WorkOrdersStatus)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/v1/work-orders/{id:guid}/history",
                (Guid id, GetWorkOrderHistoryHandler handler, CancellationToken ct) =>
                    handler.HandleAsync(id, ct))
            .RequirePermission(Permission.WorkOrdersRead);

        app.MapGet("/api/v1/work-orders/{id:guid}/pdf",
                (Guid id, GetWorkOrderPdfHandler handler, CancellationToken ct) =>
                    PdfAsync(id, handler, ct))
            .RequirePermission(Permission.WorkOrdersRead);

        app.MapGet("/api/v1/work-orders/{id:guid}/public-link",
                (Guid id, GetWorkOrderPublicLinkHandler handler, CancellationToken ct) =>
                    PublicLinkAsync(id, handler, ct))
            .RequirePermission(Permission.WorkOrdersRead);

        return app;
    }

    private static async Task<IResult> SearchAsync(
        SearchWorkOrdersHandler handler,
        IAuthRepository auth,
        HttpContext ctx,
        WorkOrderStatus? status, Guid? mechanicId, Guid? vehicleId,
        bool? withBalance, DateTimeOffset? from, DateTimeOffset? to,
        AssignmentFilter assignment,
        SortSpec sort,
        int page, int pageSize, CancellationToken ct)
    {
        var (isMechanic, membershipId, includePrices) = await ResolveSessionAsync(auth, ctx, ct);
        var result = await handler.HandleAsync(
            status, mechanicId, vehicleId, withBalance, from, to, assignment,
            isMechanic, membershipId, includePrices, page, pageSize, sort, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateAsync(
        CreateWorkOrderRequest request,
        CreateWorkOrderHandler handler,
        CancellationToken ct)
    {
        var dto = await handler.HandleAsync(request, ct);
        return Results.Created($"/api/v1/work-orders/{dto.Id}", dto);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        GetWorkOrderHandler handler,
        IAuthRepository auth,
        HttpContext ctx,
        CancellationToken ct)
    {
        var (isMechanic, membershipId, includePrices) = await ResolveSessionAsync(auth, ctx, ct);
        var dto = await handler.HandleAsync(id, isMechanic, membershipId, includePrices, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateWorkOrderRequest request,
        UpdateWorkOrderHandler handler,
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

        var dto = await handler.HandleAsync(id, request, version, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> ChangeStatusAsync(
        Guid id,
        ChangeStatusRequest request,
        ChangeWorkOrderStatusHandler handler,
        IAuthRepository auth,
        HttpContext ctx,
        CancellationToken ct)
    {
        var membership = await ResolveMembershipAsync(auth, ctx, ct);
        var isMechanic = membership?.Membership.Role == TenantRole.Mechanic;
        var canDeliver = membership?.Membership.EffectivePermissions().Contains(Permission.WorkOrdersDeliver) ?? false;
        var canCancel = membership?.Membership.EffectivePermissions().Contains(Permission.WorkOrdersCancel) ?? false;

        var dto = await handler.HandleAsync(id, request, isMechanic, canDeliver, canCancel, ct);
        return Results.Ok(dto);
    }

    private static async Task<IResult> AssignAsync(
        Guid id,
        AssignMechanicRequest request,
        AssignMechanicHandler handler,
        IAuthRepository auth,
        HttpContext ctx,
        CancellationToken ct)
    {
        var (isMechanic, membershipId, _) = await ResolveSessionAsync(auth, ctx, ct);
        await handler.HandleAsync(id, request, isMechanic, membershipId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> UpdateMechanicNotesAsync(
        Guid id,
        UpdateMechanicNotesRequest request,
        UpdateMechanicNotesHandler handler,
        IAuthRepository auth,
        HttpContext ctx,
        CancellationToken ct)
    {
        var membership = await ResolveMembershipAsync(auth, ctx, ct);
        var mechanicMembershipId = membership?.Membership.Id ?? Guid.Empty;

        await handler.HandleAsync(id, request, mechanicMembershipId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> PdfAsync(
        Guid id,
        GetWorkOrderPdfHandler handler,
        CancellationToken ct)
    {
        var bytes = await handler.HandleAsync(id, ct);
        return Results.File(bytes, "application/pdf", $"orden-{id}.pdf");
    }

    private static async Task<IResult> PublicLinkAsync(
        Guid id,
        GetWorkOrderPublicLinkHandler handler,
        CancellationToken ct)
    {
        var dto = await handler.HandleAsync(id, ct);
        return Results.Ok(dto);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<(bool IsMechanic, Guid? MembershipId, bool IncludePrices)> ResolveSessionAsync(
        IAuthRepository auth, HttpContext ctx, CancellationToken ct)
    {
        var membership = await ResolveMembershipAsync(auth, ctx, ct);
        if (membership is null) return (false, null, false);

        var isMechanic = membership.Membership.Role == TenantRole.Mechanic;
        var includePrices = membership.Membership.EffectivePermissions().Contains(Permission.PricesView);
        return (isMechanic, membership.Membership.Id, includePrices);
    }

    private static async Task<MembershipWithTenant?> ResolveMembershipAsync(
        IAuthRepository auth, HttpContext ctx, CancellationToken ct)
    {
        var raw = ctx.User.FindFirst(SessionClaims.UserId)?.Value;
        if (!Guid.TryParse(raw, out var userId)) return null;
        return await auth.FindActiveMembershipAsync(userId, ct);
    }

    private static bool TryParseETag(string? raw, out uint version)
    {
        version = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        return uint.TryParse(raw.Trim('"'), out version);
    }
}
