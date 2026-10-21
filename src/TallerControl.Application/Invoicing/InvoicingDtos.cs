using TallerControl.Domain.Invoicing;

namespace TallerControl.Application.Invoicing;

// ── Certificates ──────────────────────────────────────────────────────────────

public sealed record FiscalCertificateDto(
    Guid Id,
    string Cuit,
    string Name,
    bool IsPrimary,
    FiscalCertificateStatus Status,
    string CsrPem,
    DateTimeOffset? ExpiresAt,
    bool IsActive,
    uint Version,
    DateTimeOffset CreatedAt);

public sealed record CreateFiscalCertificateRequest(string Cuit, string Name, bool IsPrimary);

public sealed record UploadFiscalCertificateRequest(string CertificatePem, DateTimeOffset ExpiresAt);

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

public sealed record UpdateFiscalPointOfSaleRequest(string Name);

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
    DateTimeOffset CreatedAt);

public sealed record InvoiceListDto(
    Guid Id,
    int? Number,
    InvoiceType InvoiceType,
    InvoiceStatus Status,
    bool IsCredit,
    decimal TotalAmount,
    DateTimeOffset IssuedAt,
    InvoiceSourceType SourceType,
    Guid SourceId);

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
    string? Cae = null);
