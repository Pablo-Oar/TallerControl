using TallerControl.Api.Identity;
using TallerControl.Application.Work;
using TallerControl.Domain.Users;
using TallerControl.Domain.Work;

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
                 RequestAttachmentUploadUrlHandler handler, CancellationToken ct) =>
                    UploadUrlAsync(request, handler, ct))
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
            .RequirePermission(Permission.AttachmentsWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapDelete("/api/v1/attachments/{id:guid}",
                (Guid id, DeleteAttachmentHandler handler, CancellationToken ct) =>
                    DeleteAsync(id, handler, ct))
            .RequirePermission(Permission.AttachmentsWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }

    private static async Task<IResult> UploadUrlAsync(
        RequestAttachmentUploadUrlRequest request,
        RequestAttachmentUploadUrlHandler handler,
        CancellationToken ct)
    {
        var dto = await handler.HandleAsync(request.OwnerType, request.OwnerId, request, ct);
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
