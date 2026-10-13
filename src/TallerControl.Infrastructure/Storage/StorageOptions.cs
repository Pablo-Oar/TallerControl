namespace TallerControl.Infrastructure.Storage;

/// <summary>
/// Conexión al almacenamiento de objetos: Cloudflare R2 en producción, RustFS en local (ambos
/// hablan el protocolo S3). Se configura por <c>appsettings</c>/variables de entorno, nunca en el
/// repo (doc 02, CLAUDE.md "Entorno del usuario").
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string ServiceUrl { get; set; } = "http://127.0.0.1:9000";

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string Bucket { get; set; } = string.Empty;

    /// <summary>RustFS y R2 esperan la ruta del bucket en el path, no en el subdominio.</summary>
    public bool ForcePathStyle { get; set; } = true;

    /// <summary>R2 usa "auto"; RustFS y los emuladores S3 locales piden una región fija.</summary>
    public string Region { get; set; } = "us-east-1";
}
