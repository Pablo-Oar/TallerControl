using TallerControl.Application.Common;

namespace TallerControl.Api.IntegrationTests.Settings;

/// <summary>
/// Reemplaza <c>S3ObjectStorage</c> en los tests: no hay RustFS real, así que "subir" un archivo es
/// simplemente llamar a <see cref="Seed"/> antes de confirmar (doc 03 §flujo de adjuntos).
/// </summary>
internal sealed class FakeObjectStorage : IObjectStorage
{
    private readonly Dictionary<string, (string ContentType, byte[] Bytes)> _objects = new();

    public string CreatePresignedPutUrl(string fileKey, string contentType, TimeSpan expiry) =>
        $"https://fake-storage.local/{fileKey}?put";

    public string CreatePresignedGetUrl(string fileKey, TimeSpan expiry) =>
        $"https://fake-storage.local/{fileKey}?get";

    public Task<StoredObject?> HeadAsync(string fileKey, CancellationToken ct) =>
        Task.FromResult(_objects.TryGetValue(fileKey, out var stored)
            ? new StoredObject(stored.Bytes.Length, stored.ContentType)
            : null);

    public Task<byte[]> ReadPrefixAsync(string fileKey, int byteCount, CancellationToken ct) =>
        Task.FromResult(_objects.TryGetValue(fileKey, out var stored)
            ? stored.Bytes.Take(byteCount).ToArray()
            : []);

    /// <summary>Simula que el navegador ya subió el archivo a la URL prefirmada.</summary>
    public void Seed(string fileKey, string contentType, byte[] bytes) => _objects[fileKey] = (contentType, bytes);
}
