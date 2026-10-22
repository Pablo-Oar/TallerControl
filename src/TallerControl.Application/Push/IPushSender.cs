namespace TallerControl.Application.Push;

/// <summary>Aviso push para un dispositivo: título, texto y la ruta a abrir al tocarlo.</summary>
public sealed record PushMessage(string Title, string Body, string? Url = null);

/// <summary>
/// Envía avisos push (Web Push con VAPID) a los dispositivos de un usuario. Si el servidor no tiene claves VAPID
/// configuradas, <see cref="PublicKey"/> es nulo y los envíos no hacen nada.
/// </summary>
public interface IPushSender
{
    /// <summary>Clave pública VAPID (base64url) para que el navegador se suscriba; nula si no está configurada.</summary>
    string? PublicKey { get; }

    /// <summary>Devuelve a cuántos dispositivos se entregó el aviso.</summary>
    Task<int> SendToUserAsync(Guid userId, PushMessage message, CancellationToken ct);
}
