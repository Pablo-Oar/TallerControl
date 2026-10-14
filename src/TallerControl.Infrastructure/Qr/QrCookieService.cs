using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using TallerControl.Application.Qr;

namespace TallerControl.Infrastructure.Qr;

internal sealed class QrCookieService : IQrCookieService
{
    private const string Purpose = "TallerControl.QrOwnerSession.v1";
    private readonly IDataProtector _protector;

    public QrCookieService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(QrSession session)
    {
        var json = JsonSerializer.Serialize(session);
        return _protector.Protect(json);
    }

    public QrSession? Unprotect(string? cookieValue)
    {
        if (string.IsNullOrEmpty(cookieValue)) return null;

        try
        {
            var json = _protector.Unprotect(cookieValue);
            return JsonSerializer.Deserialize<QrSession>(json);
        }
        catch
        {
            return null;
        }
    }
}
