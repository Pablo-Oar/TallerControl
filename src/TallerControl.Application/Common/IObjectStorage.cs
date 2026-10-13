namespace TallerControl.Application.Common;

/// <summary>Metadatos de un objeto ya almacenado, leídos con un HEAD (doc 03 §flujo de adjuntos).</summary>
public sealed record StoredObject(long Size, string? ContentType);

/// <summary>
/// Almacenamiento de objetos (Cloudflare R2 en producción, RustFS en local; ambos hablan S3).
/// El cliente nunca elige la clave: la calcula el servidor y las subidas van por URL prefirmada
/// (PUT 5 min, GET 10 min), como exige la regla de adjuntos de doc 07 §5.
/// </summary>
public interface IObjectStorage
{
    /// <summary>URL PUT prefirmada para que el navegador suba directo a <paramref name="fileKey"/>.</summary>
    string CreatePresignedPutUrl(string fileKey, string contentType, TimeSpan expiry);

    /// <summary>URL GET prefirmada para leer <paramref name="fileKey"/>.</summary>
    string CreatePresignedGetUrl(string fileKey, TimeSpan expiry);

    /// <summary>Metadatos del objeto, o nulo si no existe.</summary>
    Task<StoredObject?> HeadAsync(string fileKey, CancellationToken ct);

    /// <summary>Primeros <paramref name="byteCount"/> bytes del objeto, para validar los magic bytes.</summary>
    Task<byte[]> ReadPrefixAsync(string fileKey, int byteCount, CancellationToken ct);
}
