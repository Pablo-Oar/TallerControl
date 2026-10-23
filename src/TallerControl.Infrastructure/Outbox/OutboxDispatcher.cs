using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TallerControl.Application.Common;
using TallerControl.Application.Push;
using TallerControl.Domain.Notifications;
using TallerControl.Domain.Outbox;
using TallerControl.Domain.Qr;
using TallerControl.Domain.Tenancy;
using TallerControl.Domain.Users;
using TallerControl.Domain.Vehicles;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Outbox;

/// <summary>
/// Procesa los mensajes del outbox cada 5 s. Despacha emails a partir de <see cref="IEmailSender"/>
/// según el tipo del mensaje. Los fallos se registran en el mensaje; no abortan el ciclo.
/// </summary>
internal sealed class OutboxDispatcher : BackgroundService
{
    // Los mensajes se guardan con nombres en camelCase y los registros del despachador van en PascalCase:
    // sin ignorar mayúsculas los campos quedaban vacíos y el aviso se descartaba sin error.
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int MaxAttempts = 5;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(IServiceScopeFactory scopeFactory, ILogger<OutboxDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error inesperado en OutboxDispatcher");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    internal async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var pending = await db.Set<OutboxMessage>()
            .IgnoreQueryFilters()
            .Where(m => m.ProcessedAt == null && m.Attempts < MaxAttempts)
            .OrderBy(m => m.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        if (pending.Count == 0)
        {
            return;
        }

        foreach (var message in pending)
        {
            try
            {
                if (message.Type.StartsWith("push.", StringComparison.Ordinal))
                {
                    await DispatchPushAsync(message, db, scope.ServiceProvider.GetRequiredService<IPushSender>(), ct);
                    message.MarkProcessed(clock.GetUtcNow());
                    continue;
                }

                var frontendBase = scope.ServiceProvider.GetRequiredService<IConfiguration>()["App:FrontendBaseUrl"]
                    ?? "http://localhost:5173";
                foreach (var email in await BuildEmailMessagesAsync(message, db, frontendBase, clock.GetUtcNow(), ct))
                {
                    await sender.SendAsync(email, ct);
                }

                message.MarkProcessed(clock.GetUtcNow());
            }
            catch (Exception ex)
            {
                message.RegisterFailure(ex.Message);
                _logger.LogWarning(ex,
                    "Fallo al procesar mensaje outbox {Id} tipo {Type} intento {Attempts}",
                    message.Id, message.Type, message.Attempts);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Avisos push. Sin push configurado no hace nada; un tipo desconocido falla, igual que con los emails.</summary>
    private static async Task DispatchPushAsync(OutboxMessage message, AppDbContext db, IPushSender push, CancellationToken ct)
    {
        switch (message.Type)
        {
            case "push.work_order_assigned":
                var payload = JsonSerializer.Deserialize<WorkOrderAssignedPayload>(message.PayloadJson, PayloadOptions);
                if (payload is null || push.PublicKey is null) return;

                var userId = await db.Set<Membership>()
                    .IgnoreQueryFilters()
                    .Where(m => m.Id == payload.MembershipId)
                    .Select(m => (Guid?)m.UserId)
                    .FirstOrDefaultAsync(ct);
                if (userId is null) return;

                var result = await push.SendToUserAsync(
                    userId.Value,
                    new PushMessage("Nueva orden asignada", $"Te asignaron la orden N° {payload.OrderNumber}.", $"/m/work-orders/{payload.WorkOrderId}",
                        $"work-order-{payload.WorkOrderId}"),
                    ct);

                // Con dispositivos suscriptos y ninguna entrega, el mensaje falla: queda el motivo en last_error y se reintenta.
                if (result.Subscriptions > 0 && result.Delivered == 0)
                    throw new InvalidOperationException("Push no entregado: " + string.Join(" | ", result.Failures));
                return;
            default:
                throw new NotSupportedException($"No hay procesador para el tipo de mensaje '{message.Type}'.");
        }
    }

    private sealed record WorkOrderAssignedPayload(Guid MembershipId, int OrderNumber, Guid WorkOrderId);

    /// <summary>
    /// Arma los emails de un mensaje (puede ser ninguno, uno o varios). Un tipo desconocido <b>falla</b> en vez de
    /// marcarse como procesado: así un aviso sin procesador no se pierde en silencio.
    /// </summary>
    private static async Task<IReadOnlyList<EmailMessage>> BuildEmailMessagesAsync(
        OutboxMessage message, AppDbContext db, string frontendBase, DateTimeOffset now, CancellationToken ct)
    {
        switch (message.Type)
        {
            case "email.invitation":
                return Single(BuildInvitationEmail(message.PayloadJson));
            case "email.password_reset":
                return Single(BuildPasswordResetEmail(message.PayloadJson));
            case "email.email_verification":
                return Single(BuildEmailVerificationEmail(message.PayloadJson));
            case "QrOtpEmail":
                return Single(BuildQrOtpEmail(message.PayloadJson));
            case "qr.found_keys_alert":
                return await BuildFoundKeysAlertAsync(message.PayloadJson, db, frontendBase, now, ct);
            case "reminder.due":
                return Single(BuildReminderEmail(message.PayloadJson));
            default:
                throw new NotSupportedException($"No hay procesador para el tipo de mensaje '{message.Type}'.");
        }

        static IReadOnlyList<EmailMessage> Single(EmailMessage? email) => email is null ? [] : [email];
    }

    private static EmailMessage? BuildQrOtpEmail(string json)
    {
        var payload = JsonSerializer.Deserialize<QrOtpPayload>(json, PayloadOptions);
        if (payload is null || string.IsNullOrWhiteSpace(payload.To)) return null;

        return new EmailMessage(
            To: payload.To,
            Subject: "Tu código de verificación — TallerControl",
            HtmlBody: $"""
                <p>Tu código para ver el historial de tu vehículo es:</p>
                <p style="font-size:28px;font-weight:bold;letter-spacing:6px">{payload.Otp}</p>
                <p>Vence el {payload.ExpiresAt:f}. Si no lo pediste vos, ignorá este email: sin el código nadie puede ver tus datos.</p>
                """);
    }

    private static EmailMessage? BuildReminderEmail(string json)
    {
        var payload = JsonSerializer.Deserialize<ReminderPayload>(json, PayloadOptions);
        // «No contactar» y sin email: el aviso queda solo en el panel del taller.
        if (payload is null || payload.DoNotContact || string.IsNullOrWhiteSpace(payload.CustomerEmail)) return null;

        var when = payload.DueDate.HasValue
            ? $"el {payload.DueDate.Value:dd/MM/yyyy}"
            : payload.DueKm.HasValue ? $"a los {payload.DueKm.Value:N0} km" : "pronto";

        return new EmailMessage(
            To: payload.CustomerEmail,
            Subject: $"Recordatorio para tu vehículo {payload.VehiclePlate}",
            HtmlBody: $"""
                <p>Hola {payload.CustomerName},</p>
                <p>Te recordamos que <b>{payload.Title}</b> de tu vehículo <b>{payload.VehiclePlate}</b> vence {when}.</p>
                <p>Comunicate con el taller para coordinar un turno.</p>
                """);
    }

    private static async Task<IReadOnlyList<EmailMessage>> BuildFoundKeysAlertAsync(
        string json, AppDbContext db, string frontendBase, DateTimeOffset now, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<FoundKeysPayload>(json, PayloadOptions);
        if (payload is null) return [];

        var card = await db.Set<QrCard>().IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == payload.QrCardId && c.TenantId == payload.TenantId, ct);
        var vehicle = card?.VehicleId is { } vehicleId
            ? await db.Set<Vehicle>().IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(v => v.Id == vehicleId, ct)
            : null;
        var plate = vehicle?.Plate ?? "un vehículo";

        // El aviso también queda en el panel (campana), para quien no mire el correo.
        db.Set<Notification>().Add(new Notification(
            payload.TenantId, "qr.found_keys", $"Encontraron las llaves de {plate}", now, membershipId: null, link: "/app/qr"));

        var tenantName = await db.Set<Tenant>().AsNoTracking()
            .Where(t => t.Id == payload.TenantId).Select(t => t.Name).FirstOrDefaultAsync(ct) ?? "tu taller";

        var recipients = await (
            from m in db.Set<Membership>().IgnoreQueryFilters().AsNoTracking()
            join u in db.Set<User>().AsNoTracking() on m.UserId equals u.Id
            where m.TenantId == payload.TenantId
                  && m.Status == MembershipStatus.Active
                  && (m.Role == TenantRole.Owner || m.Role == TenantRole.AdminStaff)
            select u.Email).Distinct().ToListAsync(ct);

        var reporter = string.IsNullOrWhiteSpace(payload.ReporterName) ? "Una persona" : payload.ReporterName;
        var contact = string.IsNullOrWhiteSpace(payload.ReporterPhone)
            ? "No dejó un teléfono."
            : $"Teléfono de contacto: <b>{payload.ReporterPhone}</b>.";

        return recipients.Select(to => new EmailMessage(
            To: to,
            Subject: $"Encontraron las llaves de {plate} — {tenantName}",
            HtmlBody: $"""
                <p>{reporter} avisó que <b>encontró las llaves del vehículo {plate}</b> (tarjeta {payload.Code}).</p>
                <p>{contact}</p>
                <p><a href="{frontendBase}/app/qr">Abrir las tarjetas QR</a></p>
                """)).ToList();
    }

    private static EmailMessage? BuildInvitationEmail(string json)
    {
        var payload = JsonSerializer.Deserialize<InvitationPayload>(json, PayloadOptions);
        if (payload is null)
        {
            return null;
        }

        var link = $"{payload.FrontendBaseUrl}/invitations/{Uri.EscapeDataString(payload.Token)}/accept";
        return new EmailMessage(
            To: payload.To,
            Subject: "Te invitaron a TallerControl",
            HtmlBody: $"""
                <p>Hola {payload.DisplayName},</p>
                <p>Fuiste invitado a un taller en TallerControl.</p>
                <p><a href="{link}">Aceptar invitación</a></p>
                <p>El enlace vence el {payload.ExpiresAt:f}.</p>
                """);
    }

    private static EmailMessage? BuildPasswordResetEmail(string json)
    {
        var payload = JsonSerializer.Deserialize<PasswordResetPayload>(json, PayloadOptions);
        if (payload is null)
        {
            return null;
        }

        var link = $"{payload.FrontendBaseUrl}/reset-password?token={Uri.EscapeDataString(payload.Token)}";
        return new EmailMessage(
            To: payload.To,
            Subject: "Restablecimiento de contraseña — TallerControl",
            HtmlBody: $"""
                <p>Recibimos una solicitud para restablecer la contraseña de tu cuenta.</p>
                <p><a href="{link}">Restablecer contraseña</a></p>
                <p>El enlace vence en 1 hora ({payload.ExpiresAt:f}). Si no solicitaste esto, ignorá este email.</p>
                """);
    }

    private static EmailMessage? BuildEmailVerificationEmail(string json)
    {
        var payload = JsonSerializer.Deserialize<EmailVerificationPayload>(json, PayloadOptions);
        if (payload is null)
        {
            return null;
        }

        var link = $"{payload.FrontendBaseUrl}/verificar-email?token={Uri.EscapeDataString(payload.Token)}";
        return new EmailMessage(
            To: payload.To,
            Subject: "Verificá tu email — TallerControl",
            HtmlBody: $"""
                <p>Gracias por registrarte en TallerControl.</p>
                <p><a href="{link}">Verificar mi email</a></p>
                <p>El enlace vence el {payload.ExpiresAt:f}. Si no te registraste, ignorá este email.</p>
                """);
    }

    // Proyecciones locales que coinciden con los payloads serializados por los handlers.
    private sealed record InvitationPayload(
        string To,
        string DisplayName,
        string Token,
        DateTimeOffset ExpiresAt,
        string FrontendBaseUrl);

    private sealed record PasswordResetPayload(
        string To,
        string Token,
        DateTimeOffset ExpiresAt,
        string FrontendBaseUrl);

    private sealed record EmailVerificationPayload(
        string To,
        string Token,
        DateTimeOffset ExpiresAt,
        string FrontendBaseUrl);

    private sealed record QrOtpPayload(string To, string Otp, DateTimeOffset ExpiresAt);

    private sealed record FoundKeysPayload(
        Guid TenantId,
        Guid QrCardId,
        string Code,
        string? ReporterName,
        string? ReporterPhone,
        DateTimeOffset ReportedAt);

    private sealed record ReminderPayload(
        Guid TenantId,
        string VehiclePlate,
        string CustomerName,
        string? CustomerEmail,
        bool DoNotContact,
        string Title,
        DateTimeOffset? DueDate,
        int? DueKm);
}
