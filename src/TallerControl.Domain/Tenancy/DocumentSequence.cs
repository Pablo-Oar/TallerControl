using TallerControl.Domain.Common;

namespace TallerControl.Domain.Tenancy;

/// <summary>
/// Contador correlativo por <c>(taller, sucursal, tipo)</c> (doc 04 §3.1 y V2 cambio 1).
/// Se modifica solo desde el generador de números, dentro de la transacción del documento.
/// </summary>
public sealed class DocumentSequence : ITenantOwned
{
    private DocumentSequence()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid BranchId { get; private set; }

    public DocumentKind Kind { get; private set; }

    public int NextValue { get; private set; } = 1;
}
