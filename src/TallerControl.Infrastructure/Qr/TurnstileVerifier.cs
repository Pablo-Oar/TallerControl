using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using TallerControl.Application.Qr;

namespace TallerControl.Infrastructure.Qr;

internal sealed class TurnstileVerifier : ITurnstileVerifier
{
    // Clave de test de Cloudflare que siempre pasa (documentada en https://developers.cloudflare.com/turnstile/troubleshooting/testing/)
    private const string AlwaysPassKey = "1x0000000000000000000000000000000AA";
    private const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    private readonly HttpClient _http;
    private readonly QrOptions _options;

    public TurnstileVerifier(HttpClient http, IOptions<QrOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<bool> VerifyAsync(string token, string? clientIp, CancellationToken ct)
    {
        // La test key siempre pasa; permite ejecutar los tests sin llamada real a Cloudflare.
        if (_options.TurnstileSecretKey == AlwaysPassKey)
            return true;

        var data = new Dictionary<string, string>
        {
            ["secret"] = _options.TurnstileSecretKey,
            ["response"] = token
        };

        if (!string.IsNullOrEmpty(clientIp))
            data["remoteip"] = clientIp;

        var response = await _http.PostAsync(VerifyUrl, new FormUrlEncodedContent(data), ct);
        if (!response.IsSuccessStatusCode) return false;

        var result = await response.Content.ReadFromJsonAsync<TurnstileResponse>(ct);
        return result?.Success is true;
    }

    private sealed record TurnstileResponse(bool Success);
}
