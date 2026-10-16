using TallerControl.Domain.Common;

namespace TallerControl.Domain.Invoicing;

/// <summary>
/// Comprobante electrónico ARCA emitido desde una orden de trabajo o venta de mostrador (E12).
/// Una orden facturada muestra el estado ARCA y sus comprobantes (doc 13 §1).
/// </summary>
public sealed class Invoice : Entity, ITenantOwned, IVersioned, IAuditable
{
    public const int CaeMaxLength = 20;
    public const int RejectionReasonMaxLength = 500;
    public const int ArcaResponseMaxLength = 4000;

    private Invoice()
    {
    }

    public Invoice(
        Guid tenantId,
        Guid fiscalPointOfSaleId,
        InvoiceSourceType sourceType,
        Guid sourceId,
        InvoiceType invoiceType,
        decimal netAmount,
        decimal vatAmount,
        decimal totalAmount,
        DateTimeOffset issuedAt,
        bool isCredit = false,
        Guid? originalInvoiceId = null)
    {
        if (isCredit && originalInvoiceId == null)
            throw new ArgumentException("Una nota de crédito debe referenciar al comprobante original.", nameof(originalInvoiceId));

        TenantId = tenantId;
        FiscalPointOfSaleId = fiscalPointOfSaleId;
        SourceType = sourceType;
        SourceId = sourceId;
        InvoiceType = invoiceType;
        NetAmount = ValidatePositive(netAmount, nameof(netAmount));
        VatAmount = vatAmount >= 0 ? vatAmount : throw new ArgumentOutOfRangeException(nameof(vatAmount), "El IVA no puede ser negativo.");
        TotalAmount = ValidatePositive(totalAmount, nameof(totalAmount));
        IssuedAt = issuedAt;
        IsCredit = isCredit;
        OriginalInvoiceId = originalInvoiceId;
        Status = InvoiceStatus.Pending;
    }

    public Guid TenantId { get; private set; }
    public Guid FiscalPointOfSaleId { get; private set; }
    public InvoiceSourceType SourceType { get; private set; }
    public Guid SourceId { get; private set; }
    public InvoiceType InvoiceType { get; private set; }

    /// <summary>Número del comprobante. Nulo hasta que ARCA lo autoriza.</summary>
    public int? Number { get; private set; }

    /// <summary>Código de Autorización Electrónico. Nulo hasta que ARCA lo autoriza.</summary>
    public string? Cae { get; private set; }
    public DateTimeOffset? CaeExpiresAt { get; private set; }
    public InvoiceStatus Status { get; private set; }

    /// <summary>True para notas de crédito.</summary>
    public bool IsCredit { get; private set; }

    /// <summary>Factura original. Solo presente en notas de crédito.</summary>
    public Guid? OriginalInvoiceId { get; private set; }

    public decimal NetAmount { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal TotalAmount { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; }

    /// <summary>Respuesta XML cruda de ARCA. Solo para diagnóstico.</summary>
    public string? ArcaResponse { get; private set; }
    public string? RejectionReason { get; private set; }

    public uint Version { get; private set; }

    // IAuditable
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    // Navigation
    public FiscalPointOfSale FiscalPointOfSale { get; private set; } = null!;

    /// <summary>Autoriza el comprobante con el CAE devuelto por ARCA.</summary>
    public void Authorize(int number, string cae, DateTimeOffset caeExpiresAt, string? arcaResponse)
    {
        if (Status != InvoiceStatus.Pending)
            throw new InvalidOperationException("Solo se pueden autorizar comprobantes pendientes.");

        if (string.IsNullOrWhiteSpace(cae))
            throw new ArgumentException("El CAE es obligatorio.", nameof(cae));

        Number = number;
        Cae = cae.Trim();
        CaeExpiresAt = caeExpiresAt;
        Status = InvoiceStatus.Authorized;

        if (arcaResponse is { Length: > 0 })
            ArcaResponse = arcaResponse.Trim()[..Math.Min(arcaResponse.Trim().Length, ArcaResponseMaxLength)];
    }

    /// <summary>Marca el comprobante como rechazado por ARCA con el motivo devuelto.</summary>
    public void Reject(string reason, string? arcaResponse)
    {
        if (Status != InvoiceStatus.Pending)
            throw new InvalidOperationException("Solo se pueden rechazar comprobantes pendientes.");

        RejectionReason = reason?.Trim();
        Status = InvoiceStatus.Rejected;

        if (arcaResponse is { Length: > 0 })
            ArcaResponse = arcaResponse.Trim()[..Math.Min(arcaResponse.Trim().Length, ArcaResponseMaxLength)];
    }

    private static decimal ValidatePositive(decimal amount, string paramName)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(paramName, "El monto debe ser mayor a 0.");
        return amount;
    }
}
