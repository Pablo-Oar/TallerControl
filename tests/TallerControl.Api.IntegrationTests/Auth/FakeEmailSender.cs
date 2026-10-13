using TallerControl.Application.Common;

namespace TallerControl.Api.IntegrationTests.Auth;

/// <summary>
/// Reemplaza <see cref="IEmailSender"/> en entorno "Testing" para capturar emails sin enviarlos.
/// Los tests leen <see cref="Sent"/> para verificar el contenido.
/// </summary>
public sealed class FakeEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _sent = [];

    public IReadOnlyList<EmailMessage> Sent => _sent;

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        _sent.Add(message);
        return Task.CompletedTask;
    }

    public void Clear() => _sent.Clear();
}
