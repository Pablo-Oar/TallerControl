using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TallerControl.Application.Common;
using TallerControl.Domain.Outbox;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Outbox;

/// <summary>
/// Procesa los mensajes del outbox cada 5 s. Despacha emails a partir de <see cref="IEmailSender"/>
/// según el tipo del mensaje. Los fallos se registran en el mensaje; no abortan el ciclo.
/// </summary>
internal sealed class OutboxDispatcher : BackgroundService
{
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

    private async Task ProcessBatchAsync(CancellationToken ct)
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
                var email = BuildEmailMessage(message);
                if (email is not null)
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

    private static EmailMessage? BuildEmailMessage(OutboxMessage message)
    {
        return message.Type switch
        {
            "email.invitation" => BuildInvitationEmail(message.PayloadJson),
            "email.password_reset" => BuildPasswordResetEmail(message.PayloadJson),
            "email.email_verification" => BuildEmailVerificationEmail(message.PayloadJson),
            _ => null,
        };
    }

    private static EmailMessage? BuildInvitationEmail(string json)
    {
        var payload = JsonSerializer.Deserialize<InvitationPayload>(json);
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
        var payload = JsonSerializer.Deserialize<PasswordResetPayload>(json);
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
        var payload = JsonSerializer.Deserialize<EmailVerificationPayload>(json);
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
}
