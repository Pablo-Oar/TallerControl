namespace TallerControl.Application.Push;

/// <summary>Aviso push para un dispositivo: título, texto y la ruta a abrir al tocarlo.</summary>
/// <param name="Tag">Avisos con la misma etiqueta se reemplazan entre sí en el dispositivo (evita duplicados).</param>
public sealed record PushMessage(string Title, string Body, string? Url = null, string? Tag = null);

/// <summary>Resultado de un envío: dispositivos del usuario, entregados y el motivo de cada falla.</summary>
public sealed record PushResult(int Subscriptions, int Delivered, IReadOnlyList<string> Failures, IReadOnlyList<int>? Statuses = null);

/// <summary>
/// Envía avisos push (Web Push con VAPID) a los dispositivos de un usuario. Si el servidor no tiene claves VAPID
/// configuradas, <see cref="PublicKey"/> es nulo y los envíos no hacen nada.
/// </summary>
public interface IPushSender
{
    /// <summary>Clave pública VAPID (base64url) para que el navegador se suscriba; nula si no está configurada.</summary>
    string? PublicKey { get; }

    Task<PushResult> SendToUserAsync(Guid userId, PushMessage message, CancellationToken ct);
}
