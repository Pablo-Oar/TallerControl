using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TallerControl.Application.Push;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Push;

/// <summary>Claves VAPID y contacto del servidor (sección <c>Push</c>). Sin claves, el push queda desactivado.</summary>
public sealed class PushOptions
{
    public const string SectionName = "Push";

    /// <summary>Clave pública VAPID, base64url (65 bytes sin comprimir).</summary>
    public string? PublicKey { get; set; }

    /// <summary>Clave privada VAPID, base64url (32 bytes). Va en user-secrets o variables de entorno, nunca en el repo.</summary>
    public string? PrivateKey { get; set; }

    /// <summary>Contacto que exige VAPID (<c>mailto:</c> o URL https).</summary>
    public string Subject { get; set; } = "mailto:soporte@tallercontrol.com.ar";
}

internal sealed class WebPushSender : IPushSender
{
    private readonly HttpClient _http;
    private readonly AppDbContext _db;
    private readonly PushOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<WebPushSender> _logger;

    public WebPushSender(
        HttpClient http, AppDbContext db, IOptions<PushOptions> options, TimeProvider clock, ILogger<WebPushSender> logger)
    {
        _http = http;
        _db = db;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    public string? PublicKey =>
        string.IsNullOrWhiteSpace(_options.PublicKey) || string.IsNullOrWhiteSpace(_options.PrivateKey)
            ? null
            : _options.PublicKey;

    public async Task<PushResult> SendToUserAsync(Guid userId, PushMessage message, CancellationToken ct)
    {
        if (PublicKey is null) return new PushResult(0, 0, []);

        var subscriptions = await _db.Database
            .SqlQuery<SubscriptionRow>($"""
                SELECT endpoint AS "Endpoint", p256dh AS "P256dh", auth AS "Auth"
                FROM push_subscriptions WHERE user_id = {userId}
                """)
            .ToListAsync(ct);

        var body = JsonSerializer.SerializeToUtf8Bytes(new { title = message.Title, body = message.Body, url = message.Url });
        var delivered = 0;
        var failures = new List<string>();

        foreach (var sub in subscriptions)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, sub.Endpoint)
                {
                    Content = new ByteArrayContent(WebPushProtocol.Encrypt(
                        body, WebPushProtocol.FromBase64Url(sub.P256dh), WebPushProtocol.FromBase64Url(sub.Auth))),
                };
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                request.Content.Headers.ContentEncoding.Add("aes128gcm");
                request.Headers.TryAddWithoutValidation("TTL", "86400");
                request.Headers.TryAddWithoutValidation("Urgency", "high");
                request.Headers.TryAddWithoutValidation("Authorization", WebPushProtocol.VapidAuthorization(
                    sub.Endpoint, _options.Subject, _options.PublicKey!, _options.PrivateKey!, _clock.GetUtcNow()));

                using var response = await _http.SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                {
                    delivered++;
                }
                else if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                {
                    // El navegador dio de baja la suscripción: se descarta.
                    await _db.Database.ExecuteSqlAsync(
                        $"DELETE FROM push_subscriptions WHERE user_id = {userId} AND endpoint = {sub.Endpoint}", ct);
                }
                else
                {
                    var detail = (await response.Content.ReadAsStringAsync(ct)).Trim();
                    var reason = $"{(int)response.StatusCode} {sub.Endpoint.Split('/')[2]}: {(detail.Length > 200 ? detail[..200] : detail)}";
                    failures.Add(reason);
                    _logger.LogWarning("El servicio push rechazó el aviso: {Reason}", reason);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or FormatException or ArgumentException or TaskCanceledException)
            {
                failures.Add($"{ex.GetType().Name}: {ex.Message}");
                _logger.LogWarning(ex, "No se pudo enviar el aviso push a una suscripción");
            }
        }

        return new PushResult(subscriptions.Count, delivered, failures);
    }

    private sealed record SubscriptionRow(string Endpoint, string P256dh, string Auth);
}
