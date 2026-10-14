namespace TallerControl.Application.Qr;

/// <summary>
/// Serializa y valida la cookie tc_qr usando Data Protection API (30 minutos).
/// La cookie se protege en Infrastructure; la Application solo conoce el payload tipado.
/// </summary>
public interface IQrCookieService
{
    string Protect(QrSession session);
    QrSession? Unprotect(string? cookieValue);
}
