using TallerControl.Domain.Tenancy;

namespace TallerControl.Application.Tenancy;

/// <summary>
/// Entrega el próximo número correlativo de un tipo de documento para la sucursal activa del taller
/// de la sesión. Debe invocarse dentro de la transacción que crea el documento: si esa transacción
/// se revierte, el número se devuelve y no quedan huecos.
/// </summary>
public interface IDocumentNumberGenerator
{
    Task<int> NextAsync(DocumentKind kind, CancellationToken cancellationToken = default);
}
