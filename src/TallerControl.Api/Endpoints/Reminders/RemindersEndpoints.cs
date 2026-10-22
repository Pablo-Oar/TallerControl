using TallerControl.Api.Identity;
using TallerControl.Application.Reminders;
using TallerControl.Domain.Users;

namespace TallerControl.Api.Endpoints.Reminders;

/// <summary>
/// Endpoints de recordatorios automáticos (E13 Bloque 3).
/// GET    /api/v1/reminders             → reminders.manage (O, A)
/// POST   /api/v1/reminders             → reminders.manage (O, A)
/// GET    /api/v1/reminders/{id}        → reminders.manage (O, A)
/// PUT    /api/v1/reminders/{id}        → reminders.manage (O, A)
/// DELETE /api/v1/reminders/{id}        → reminders.manage (O, A)
/// POST   /api/v1/reminders/{id}/complete|reopen → reminders.manage (O, A)
/// POST   /api/v1/reminders/evaluate    → reminders.manage (O, A)
/// </summary>
internal static class RemindersEndpoints
{
    public static IEndpointRouteBuilder MapRemindersEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/reminders",
                (SearchRemindersHandler handler, CancellationToken ct,
                 Guid? vehicleId = null, string? status = null, int page = 1, int pageSize = 25) =>
                    handler.HandleAsync(vehicleId, status, page, pageSize, ct))
            .RequirePermission(Permission.RemindersManage);

        app.MapPost("/api/v1/reminders",
                (UpsertReminderRequest request, CreateReminderHandler handler, CancellationToken ct) =>
                    CreateAsync(request, handler, ct))
            .RequirePermission(Permission.RemindersManage)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/v1/reminders/{id:guid}",
                (Guid id, GetReminderHandler handler, CancellationToken ct) =>
                    handler.HandleAsync(id, ct))
            .RequirePermission(Permission.RemindersManage);

        app.MapPut("/api/v1/reminders/{id:guid}",
                (Guid id, UpsertReminderRequest request, UpdateReminderHandler handler,
                 HttpRequest httpRequest, CancellationToken ct) =>
                    UpdateAsync(id, request, handler, httpRequest, ct))
            .RequirePermission(Permission.RemindersManage)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapDelete("/api/v1/reminders/{id:guid}",
                (Guid id, DeleteReminderHandler handler, CancellationToken ct) =>
                    DeleteAsync(id, handler, ct))
            .RequirePermission(Permission.RemindersManage)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/reminders/{id:guid}/complete",
                async (Guid id, CompleteReminderHandler handler, CancellationToken ct) =>
                {
                    await handler.CompleteAsync(id, ct);
                    return Results.NoContent();
                })
            .RequirePermission(Permission.RemindersManage)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/reminders/{id:guid}/reopen",
                async (Guid id, CompleteReminderHandler handler, CancellationToken ct) =>
                {
                    await handler.ReopenAsync(id, ct);
                    return Results.NoContent();
                })
            .RequirePermission(Permission.RemindersManage)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/reminders/evaluate",
                (EvaluateDueRemindersHandler handler, CancellationToken ct) =>
                    handler.HandleAsync(ct))
            .RequirePermission(Permission.RemindersManage)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        return app;
    }

    private static async Task<IResult> CreateAsync(
        UpsertReminderRequest request,
        CreateReminderHandler handler,
        CancellationToken ct)
    {
        var dto = await handler.HandleAsync(request, ct);
        return Results.Created($"/api/v1/reminders/{dto.Id}", dto);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpsertReminderRequest request,
        UpdateReminderHandler handler,
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
        DeleteReminderHandler handler,
        CancellationToken ct)
    {
        await handler.HandleAsync(id, ct);
        return Results.NoContent();
    }

    private static bool TryParseETag(string? raw, out uint version)
    {
        version = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        return uint.TryParse(raw.Trim('"'), out version);
    }
}
