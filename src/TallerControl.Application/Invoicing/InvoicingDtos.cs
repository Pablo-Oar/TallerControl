using TallerControl.Domain.Invoicing;

namespace TallerControl.Application.Invoicing;

// ── Certificates ──────────────────────────────────────────────────────────────

public sealed record FiscalCertificateDto(
    Guid Id,
    string Cuit,
    string Name,
    bool IsPrimary,
    FiscalCertificateStatus Status,
    FiscalEnvironment Environment,
    string CsrPem,
    DateTimeOffset? ExpiresAt,
    bool IsActive,
    uint Version,
    DateTimeOffset CreatedAt);

public sealed record CreateFiscalCertificateRequest(
    string Cuit,
    string Name,
    bool IsPrimary,
    FiscalEnvironment Environment = FiscalEnvironment.Production);

/// <summary>El vencimiento se lee del propio certificado; <c>ExpiresAt</c> solo se usa si no se puede leer.</summary>
public sealed record UploadFiscalCertificateRequest(string CertificatePem, DateTimeOffset? ExpiresAt = null);

public sealed record TestFiscalConnectionResponse(
    bool Success,
    int? LastInvoiceNumber,
    string? ErrorMessage,
    DateTimeOffset TestedAt);

// ── Points of Sale ─────────────────────────────────────────────────────────────

public sealed record FiscalPointOfSaleDto(
    Guid Id,
    Guid FiscalCertificateId,
    int Number,
    string Name,
    bool IsActive,
    uint Version);

public sealed record CreateFiscalPointOfSaleRequest(Guid FiscalCertificateId, int Number, string Name);

/// <summary><c>IsActive</c> nulo deja el estado como está.</summary>
public sealed record UpdateFiscalPointOfSaleRequest(string Name, bool? IsActive = null);

// ── Invoices ──────────────────────────────────────────────────────────────────

public sealed record InvoiceDto(
    Guid Id,
    Guid FiscalPointOfSaleId,
    int FiscalPointOfSaleNumber,
    InvoiceSourceType SourceType,
    Guid SourceId,
    InvoiceType InvoiceType,
    int? Number,
    string? Cae,
    DateTimeOffset? CaeExpiresAt,
    InvoiceStatus Status,
    bool IsCredit,
    Guid? OriginalInvoiceId,
    decimal NetAmount,
    decimal VatAmount,
    decimal TotalAmount,
    DateTimeOffset IssuedAt,
    string? RejectionReason,
    DateTimeOffset CreatedAt,
    bool IsHomologation);

public sealed record InvoiceListDto(
    Guid Id,
    int? Number,
    InvoiceType InvoiceType,
    InvoiceStatus Status,
    bool IsCredit,
    decimal TotalAmount,
    DateTimeOffset IssuedAt,
    InvoiceSourceType SourceType,
    Guid SourceId,
    bool IsHomologation);

public sealed record EmitInvoiceRequest(Guid FiscalPointOfSaleId, int Concept = 2); // 2=Servicios

// ── IVA Book / CITI ───────────────────────────────────────────────────────────

public sealed record IvaBookDto(
    int Year,
    int Month,
    IvaBookSalesDto Sales,
    IvaBookPurchasesDto Purchases);

public sealed record IvaBookSalesDto(
    int Count,
    decimal TotalNet,
    decimal TotalVat,
    decimal TotalAmount,
    IReadOnlyList<IvaBookEntryDto> Entries);

public sealed record IvaBookPurchasesDto(
    int Count,
    decimal TotalNet,
    decimal TotalVat,
    decimal TotalAmount,
    IReadOnlyList<IvaBookEntryDto> Entries);

public sealed record IvaBookEntryDto(
    DateTimeOffset Date,
    string? InvoiceNumber,
    string? CounterpartyCuit,
    string CounterpartyName,
    decimal NetAmount,
    decimal VatAmount,
    decimal TotalAmount,
    string? Observations,
    string? InvoiceType = null,
    string? Cae = null,
    // Datos para los registros oficiales del CITI (no viajan al navegador).
    [property: System.Text.Json.Serialization.JsonIgnore] bool IsCredit = false,
    [property: System.Text.Json.Serialization.JsonIgnore] string? CounterpartyDocType = null,
    [property: System.Text.Json.Serialization.JsonIgnore] decimal Perceptions = 0,
    [property: System.Text.Json.Serialization.JsonIgnore] IReadOnlyList<IvaRateLineDto>? RateLines = null);

/// <summary>Neto e IVA de un comprobante para una misma alícuota (<c>VatRate</c> en porcentaje).</summary>
public sealed record IvaRateLineDto(decimal VatRate, decimal Net, decimal Vat);
