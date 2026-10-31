using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TallerControl.Application.Common;

namespace TallerControl.Infrastructure.Email;

/// <summary>
/// Envío de email vía Gmail SMTP (D-04 provisoria). Configuración via sección <c>Email:Smtp</c>.
/// Detrás de <see cref="IEmailSender"/> para poder cambiar de proveedor sin tocar el negocio.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _user;
    private readonly string _password;
    private readonly string _from;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        var section = configuration.GetSection("Email:Smtp");
        _host = section["Host"] ?? "smtp.gmail.com";
        _port = int.TryParse(section["Port"], out var p) ? p : 587;
        _user = section["User"]
            ?? throw new InvalidOperationException("Falta Email:Smtp:User.");
        _password = section["Password"]
            ?? throw new InvalidOperationException("Falta Email:Smtp:Password.");
        _from = section["From"]
            ?? throw new InvalidOperationException("Falta Email:Smtp:From.");
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        using var client = new SmtpClient(_host, _port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_user, _password),
        };

        // El remitente técnico es siempre la cuenta configurada (D-04); el nombre visible puede ser el del taller
        // y las respuestas pueden ir a una persona del taller (Reply-To).
        using var mail = new MailMessage(
            string.IsNullOrWhiteSpace(message.FromName) ? new MailAddress(_from) : new MailAddress(_from, message.FromName),
            new MailAddress(message.To))
        {
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true,
        };

        if (!string.IsNullOrWhiteSpace(message.ReplyTo))
        {
            mail.ReplyToList.Add(new MailAddress(message.ReplyTo));
        }

        await client.SendMailAsync(mail, ct);

        _logger.LogInformation("Email enviado a {To} asunto {Subject}", message.To, message.Subject);
    }
}
