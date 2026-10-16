using TallerControl.Api.Identity;
using TallerControl.Api.Security;
using TallerControl.Application.Scheduling;
using TallerControl.Domain.Users;

namespace TallerControl.Api.Endpoints.Appointments;

/// <summary>
/// Endpoints de agenda y turnos (E14, F-33).
/// GET    /api/v1/appointments                     → appointments.read
/// POST   /api/v1/appointments                     → appointments.write
/// GET    /api/v1/appointments/{id}                → appointments.read
/// PUT    /api/v1/appointments/{id}                → appointments.write
/// DELETE /api/v1/appointments/{id}                → appointments.cancel
/// POST   /api/v1/appointments/{id}/to-work-order  → appointments.write + workorders.write
/// GET    /api/v1/availability                     → appointments.read
/// GET    /api/v1/availability/rules               → appointments.write
/// POST   /api/v1/availability/rules               → appointments.write
/// PUT    /api/v1/availability/rules/{id}          → appointments.write
/// DELETE /api/v1/availability/rules/{id}          → appointments.write
/// GET    /api/v1/availability/exceptions          → appointments.write
/// POST   /api/v1/availability/exceptions          → appointments.write
/// DELETE /api/v1/availability/exceptions/{id}    → appointments.write
/// GET    /api/public/v1/appointments/{token}/confirm → anónimo
/// </summary>
internal static class AppointmentsEndpoints
{
    public static IEndpointRouteBuilder MapAppointmentsEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Appointments ──────────────────────────────────────────────────────

        app.MapGet("/api/v1/appointments",
                (GetAppointmentsHandler handler, CancellationToken ct,
                 string? date = null, Guid? mechanicMembershipId = null,
                 string? status = null, int page = 1, int pageSize = 25) =>
                {
                    DateOnly? parsedDate = null;
                    if (!string.IsNullOrEmpty(date) && DateOnly.TryParse(date, out var d))
                        parsedDate = d;
                    return handler.HandleAsync(parsedDate, mechanicMembershipId, status, page, pageSize, ct);
                })
            .RequirePermission(Permission.AppointmentsRead);

        app.MapPost("/api/v1/appointments",
                (CreateAppointmentRequest request, CreateAppointmentHandler handler, CancellationToken ct) =>
                    CreateAsync(request, handler, ct))
            .RequirePermission(Permission.AppointmentsWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/v1/appointments/{id:guid}",
                (Guid id, GetAppointmentHandler handler, CancellationToken ct) =>
                    handler.HandleAsync(id, ct))
            .RequirePermission(Permission.AppointmentsRead);

        app.MapPut("/api/v1/appointments/{id:guid}",
                (Guid id, UpdateAppointmentRequest request, UpdateAppointmentHandler handler,
                 HttpRequest httpRequest, CancellationToken ct) =>
                    UpdateAsync(id, request, handler, httpRequest, ct))
            .RequirePermission(Permission.AppointmentsWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapDelete("/api/v1/appointments/{id:guid}",
                (Guid id, [Microsoft.AspNetCore.Mvc.FromBody] CancelAppointmentRequest request,
                 CancelAppointmentHandler handler, CancellationToken ct) =>
                    CancelAsync(id, request, handler, ct))
            .RequirePermission(Permission.AppointmentsCancel)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/v1/appointments/{id:guid}/to-work-order",
                (Guid id, ConvertToWorkOrderRequest request,
                 ConvertAppointmentToWorkOrderHandler handler, CancellationToken ct) =>
                    ConvertAsync(id, request, handler, ct))
            .RequirePermission(Permission.AppointmentsWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        // ── Availability ──────────────────────────────────────────────────────

        app.MapGet("/api/v1/availability",
                (GetAvailabilitySlotsHandler handler, CancellationToken ct,
                 string? date = null, int requiredMinutes = 60) =>
                {
                    DateOnly parsedDate = date is not null && DateOnly.TryParse(date, out var d)
                        ? d : DateOnly.FromDateTime(DateTime.UtcNow);
                    return handler.HandleAsync(parsedDate, requiredMinutes, ct);
                })
            .RequirePermission(Permission.AppointmentsRead);

        app.MapGet("/api/v1/availability/rules",
                (ManageAvailabilityHandler handler, CancellationToken ct,
                 Guid? mechanicMembershipId = null) =>
                    handler.GetRulesAsync(mechanicMembershipId, ct))
            .RequirePermission(Permission.AppointmentsWrite);

        app.MapPost("/api/v1/availability/rules",
                (UpsertAvailabilityRuleRequest request, ManageAvailabilityHandler handler,
                 CancellationToken ct) =>
                    CreateRuleAsync(request, handler, ct))
            .RequirePermission(Permission.AppointmentsWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPut("/api/v1/availability/rules/{id:guid}",
                (Guid id, UpsertAvailabilityRuleRequest request, ManageAvailabilityHandler handler,
                 CancellationToken ct) =>
                    handler.UpdateRuleAsync(id, request, ct))
            .RequirePermission(Permission.AppointmentsWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapDelete("/api/v1/availability/rules/{id:guid}",
                (Guid id, ManageAvailabilityHandler handler, CancellationToken ct) =>
                    DeleteRuleAsync(id, handler, ct))
            .RequirePermission(Permission.AppointmentsWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/v1/availability/exceptions",
                (ManageAvailabilityHandler handler, CancellationToken ct,
                 string? date = null) =>
                {
                    DateOnly parsedDate = date is not null && DateOnly.TryParse(date, out var d)
                        ? d : DateOnly.FromDateTime(DateTime.UtcNow);
                    return handler.GetExceptionsAsync(parsedDate, ct);
                })
            .RequirePermission(Permission.AppointmentsWrite);

        app.MapPost("/api/v1/availability/exceptions",
                (CreateAvailabilityExceptionRequest request, ManageAvailabilityHandler handler,
                 CancellationToken ct) =>
                    CreateExceptionAsync(request, handler, ct))
            .RequirePermission(Permission.AppointmentsWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapDelete("/api/v1/availability/exceptions/{id:guid}",
                (Guid id, ManageAvailabilityHandler handler, CancellationToken ct) =>
                    DeleteExceptionAsync(id, handler, ct))
            .RequirePermission(Permission.AppointmentsWrite)
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        // ── Confirmación pública (link enviado al cliente) ────────────────────

        app.MapGet("/api/public/v1/appointments/{token}/confirm",
                (string token, ConfirmAppointmentPublicHandler handler, CancellationToken ct) =>
                    ConfirmPublicAsync(token, handler, ct))
            .AllowAnonymous()
            .WithPublicSecurity();

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateAppointmentRequest request,
        CreateAppointmentHandler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(request, ct);
        return Results.Created($"/api/v1/appointments/{result.Appointment.Id}", result);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateAppointmentRequest request,
        UpdateAppointmentHandler handler,
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

    private static async Task<IResult> CancelAsync(
        Guid id,
        CancelAppointmentRequest request,
        CancelAppointmentHandler handler,
        CancellationToken ct)
    {
        await handler.HandleAsync(id, request, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ConvertAsync(
        Guid id,
        ConvertToWorkOrderRequest request,
        ConvertAppointmentToWorkOrderHandler handler,
        CancellationToken ct)
    {
        var workOrder = await handler.HandleAsync(id, request, ct);
        return Results.Ok(workOrder);
    }

    private static async Task<IResult> CreateRuleAsync(
        UpsertAvailabilityRuleRequest request,
        ManageAvailabilityHandler handler,
        CancellationToken ct)
    {
        var dto = await handler.CreateRuleAsync(request, ct);
        return Results.Created($"/api/v1/availability/rules/{dto.Id}", dto);
    }

    private static async Task<IResult> DeleteRuleAsync(
        Guid id,
        ManageAvailabilityHandler handler,
        CancellationToken ct)
    {
        await handler.DeleteRuleAsync(id, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> CreateExceptionAsync(
        CreateAvailabilityExceptionRequest request,
        ManageAvailabilityHandler handler,
        CancellationToken ct)
    {
        var dto = await handler.CreateExceptionAsync(request, ct);
        return Results.Created($"/api/v1/availability/exceptions/{dto.Id}", dto);
    }

    private static async Task<IResult> DeleteExceptionAsync(
        Guid id,
        ManageAvailabilityHandler handler,
        CancellationToken ct)
    {
        await handler.DeleteExceptionAsync(id, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ConfirmPublicAsync(
        string token,
        ConfirmAppointmentPublicHandler handler,
        CancellationToken ct)
    {
        await handler.HandleAsync(token, ct);
        return Results.Ok(new { message = "Turno confirmado exitosamente." });
    }

    private static bool TryParseETag(string? raw, out uint version)
    {
        version = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        return uint.TryParse(raw.Trim('"'), out version);
    }
}
