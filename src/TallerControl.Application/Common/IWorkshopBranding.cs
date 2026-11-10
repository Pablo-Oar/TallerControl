namespace TallerControl.Application.Common;

/// <summary>Nombre del taller y enlace temporal a su logo, para las pantallas públicas.</summary>
public sealed record WorkshopBranding(string Name, string? LogoUrl);

/// <summary>Imagen del logo ya leída del almacenamiento (para incrustarla en un PDF o un email).</summary>
public sealed record LogoImage(byte[] Bytes, string ContentType);

/// <summary>
/// Marca del taller (logo cargado en Configuración) usada en los documentos que ve el cliente: PDF, páginas
/// públicas y emails. Nunca falla por culpa del logo: si no hay, no se puede leer o no es una imagen válida,
/// el documento sale igual sin él.
/// </summary>
public interface IWorkshopBranding
{
    /// <summary>Nombre y URL prefirmada (10 min) del logo; sirve en superficies sin sesión.</summary>
    Task<WorkshopBranding> GetPublicAsync(Guid tenantId, CancellationToken ct);

    /// <summary>Bytes del logo, o nulo si el taller no tiene o no se pudo leer.</summary>
    Task<LogoImage?> GetLogoAsync(Guid tenantId, CancellationToken ct);
}
