namespace TallerControl.Application.Invoicing;

/// <summary>
/// Adaptador aislado para los servicios web de ARCA (WSAA + WSFEv1).
/// RT-09: cambios regulatorios frecuentes; toda la lógica SOAP/XML vive aquí.
/// </summary>
public interface IArcaClient
{
    /// <summary>
    /// Obtiene (o renueva desde caché) el ticket de autorización de WSAA.
    /// </summary>
    Task<ArcaTicket> GetTicketAsync(ArcaCertificateCredentials credentials, CancellationToken ct);

    /// <summary>
    /// Emite uno o más comprobantes con FECAESolicitar.
    /// </summary>
    Task<ArcaAuthorizeResult> AuthorizeInvoiceAsync(ArcaTicket ticket, ArcaInvoiceRequest request, CancellationToken ct);

    /// <summary>
    /// Devuelve el último número de comprobante autorizado para el punto de venta y tipo.
    /// </summary>
    Task<int> GetLastAuthorizedNumberAsync(ArcaTicket ticket, int pointOfSaleNumber, int invoiceTypeCode, CancellationToken ct);
}

/// <summary>Credenciales para WSAA: clave privada descifrada + certificado PEM.</summary>
public sealed record ArcaCertificateCredentials(byte[] PrivateKeyDer, string CertificatePem, string Cuit);

/// <summary>Ticket de acceso devuelto por WSAA (TA). Tiene ~12h de vigencia. Incluye el CUIT del emisor.</summary>
public sealed record ArcaTicket(string Token, string Sign, string Cuit, DateTimeOffset GenerationTime, DateTimeOffset ExpirationTime);

/// <summary>Datos del comprobante a emitir vía FECAESolicitar.</summary>
public sealed record ArcaInvoiceRequest(
    string TenantCuit,
    int PointOfSaleNumber,
    int InvoiceTypeCode,
    int Concept,             // 1=Productos, 2=Servicios, 3=Productos y Servicios
    string? ReceptorCuit,    // Null si consumidor final
    int ReceptorVatConditionId,
    decimal NetAmount,
    decimal VatAmount,
    decimal TotalAmount,
    DateTimeOffset IssuedAt);

/// <summary>Resultado de FECAESolicitar.</summary>
public sealed record ArcaAuthorizeResult(
    bool IsAuthorized,
    int Number,
    string? Cae,
    DateTimeOffset? CaeExpiresAt,
    string? RejectionReason,
    string RawResponse);
