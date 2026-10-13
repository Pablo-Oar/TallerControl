namespace TallerControl.Application.Common;

/// <summary>
/// Envío de email transaccional (D-04: Gmail SMTP con contraseña de aplicación detrás de esta interfaz).
/// Siempre se llama desde el procesador del outbox, nunca directamente desde los handlers.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

/// <summary>Mensaje de email listo para enviar.</summary>
public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody);
