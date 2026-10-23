using TallerControl.Api.Identity;
using TallerControl.Application.Auth;
using TallerControl.Application.Work;
using TallerControl.Domain.Users;
using TallerControl.Domain.Work;
using TallerControl.Infrastructure.Identity;

namespace TallerControl.Api.Endpoints.Work;

/// <summary>
/// Endpoints de adjuntos (doc 05 §2.8, T-ATT-01..07).
/// POST   /api/v1/attachments/upload-url              → attachments.write (O, A, M)
/// POST   /api/v1/attachments/{id}/confirm            → attachments.write (O, A, M)
/// GET    /api/v1/work-orders/{woId}/attachments      → workorders.read   (O, A, M*)
/// PATCH  /api/v1/attachments/{id}                   → attachments.write (O, A)
/// DELETE /api/v1/attachments/{id}                   → attachments.write (O, A)
/// </summary>
internal static class AttachmentsEndpoints
{
    public static IEndpointRouteBuilder MapAttachmentsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/attachments/upload-url",
                (RequestAttachmentUploadUrlRequest request,
                 RequestAttachmentUploadUrlHandler handler, IAuthRepository auth, HttpContext ctx, CancellationToken ct) =>
                    UploadUrlAsync(request, handler, auth, ctx, ct))
            .RequirePermission(Permission.AttachmentsWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/attachments/{id:guid}/confirm",
                (Guid id, ConfirmAttachmentHandler handler, CancellationToken ct) =>
                    handler.HandleAsync(id, ct))
            .RequirePermission(Permission.AttachmentsWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/v1/work-orders/{woId:guid}/attachments",
                (Guid woId, ListAttachmentsHandler handler, CancellationToken ct) =>
                    handler.HandleAsync(AttachmentOwnerType.WorkOrder, woId, ct))
            .RequirePermission(Permission.WorkOrdersRead);

        app.MapPatch("/api/v1/attachments/{id:guid}",
                (Guid id, UpdateAttachmentRequest request, UpdateAttachmentHandler handler, CancellationToken ct) =>
                    handler.HandleAsync(id, request, ct))
            .RequirePermission(Permission.AttachmentsManage)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapDelete("/api/v1/attachments/{id:guid}",
                (Guid id, DeleteAttachmentHandler handler, CancellationToken ct) =>
                    DeleteAsync(id, handler, ct))
            .RequirePermission(Permission.AttachmentsManage)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }

    private static async Task<IResult> UploadUrlAsync(
        RequestAttachmentUploadUrlRequest request,
        RequestAttachmentUploadUrlHandler handler,
        IAuthRepository auth,
        HttpContext ctx,
        CancellationToken ct)
    {
        Guid? mechanicMembershipId = null;
        if (Guid.TryParse(ctx.User.FindFirst(SessionClaims.UserId)?.Value, out var userId))
        {
            var membership = await auth.FindActiveMembershipAsync(userId, ct);
            if (membership?.Membership.Role == TenantRole.Mechanic)
                mechanicMembershipId = membership.Membership.Id;
        }

        var dto = await handler.HandleAsync(request.OwnerType, request.OwnerId, request, ct, mechanicMembershipId);
        return Results.Ok(dto);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        DeleteAttachmentHandler handler,
        CancellationToken ct)
    {
        await handler.HandleAsync(id, ct);
        return Results.NoContent();
    }
}
