using TallerControl.Domain.Common;

namespace TallerControl.Domain.Purchases;

/// <summary>
/// Compra a un proveedor. Crea StockMovements al confirmarse.
/// Campos fiscales (D-19): point_of_sale, invoice_number, perceptions, retentions.
/// Una compra sin PoS o número queda fuera del CITI (T-PUR-03).
/// </summary>
public sealed class Purchase : Entity, ITenantOwned, IVersioned, IAuditable, ISoftDeletable
{
    private readonly List<PurchaseItem> _items = [];

    private Purchase()
    {
    }

    public Purchase(
        Guid tenantId,
        Guid branchId,
        Guid supplierId,
        DateTimeOffset invoiceDate,
        int? pointOfSale,
        int? invoiceNumber,
        decimal perceptions,
        decimal retentions,
        int documentNumber)
    {
        if (perceptions < 0)
            throw new ArgumentOutOfRangeException(nameof(perceptions), "Las percepciones no pueden ser negativas.");
        if (retentions < 0)
            throw new ArgumentOutOfRangeException(nameof(retentions), "Las retenciones no pueden ser negativas.");

        TenantId = tenantId;
        BranchId = branchId;
        SupplierId = supplierId;
        InvoiceDate = invoiceDate;
        PointOfSale = pointOfSale;
        InvoiceNumber = invoiceNumber;
        Perceptions = perceptions;
        Retentions = retentions;
        DocumentNumber = documentNumber;
        Status = PurchaseStatus.Draft;
    }

    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid SupplierId { get; private set; }
    public int DocumentNumber { get; private set; }
    public DateTimeOffset InvoiceDate { get; private set; }
    public int? PointOfSale { get; private set; }
    public int? InvoiceNumber { get; private set; }
    public decimal SubtotalNet { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal Perceptions { get; private set; }
    public decimal Retentions { get; private set; }
    public decimal Total { get; private set; }
    public PurchaseStatus Status { get; private set; }
    public Guid? AccountId { get; private set; }

    /// <summary>Motivo y momento de la anulación (una compra confirmada se anula con contraasiento de stock y de caja).</summary>
    public string? CancelReason { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public uint Version { get; private set; }

    // IAuditable
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    // ISoftDeletable
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }
    public string? DeleteReason { get; private set; }

    public bool IsDeleted => DeletedAt.HasValue;

    /// <summary>
    /// Queda fuera del CITI si no tiene punto de venta o número de factura (D-19, T-PUR-03).
    /// </summary>
    public bool IsExcludedFromCiti => PointOfSale is null || InvoiceNumber is null;

    public IReadOnlyList<PurchaseItem> Items => _items.AsReadOnly();

    public void AddItem(PurchaseItem item)
    {
        if (Status != PurchaseStatus.Draft)
            throw new InvalidOperationException("Solo se pueden agregar ítems a una compra en borrador.");
        _items.Add(item);
        RecalculateTotals();
    }

    /// <summary>Corrige los datos de la factura de una compra en borrador.</summary>
    public void UpdateHeader(
        DateTimeOffset invoiceDate, int? pointOfSale, int? invoiceNumber, decimal perceptions, decimal retentions)
    {
        if (Status != PurchaseStatus.Draft)
            throw new InvalidOperationException("Solo se puede editar una compra en borrador.");
        if (perceptions < 0)
            throw new ArgumentOutOfRangeException(nameof(perceptions), "Las percepciones no pueden ser negativas.");
        if (retentions < 0)
            throw new ArgumentOutOfRangeException(nameof(retentions), "Las retenciones no pueden ser negativas.");

        InvoiceDate = invoiceDate;
        PointOfSale = pointOfSale;
        InvoiceNumber = invoiceNumber;
        Perceptions = perceptions;
        Retentions = retentions;
        RecalculateTotals();
    }

    /// <summary>Vacía los renglones de un borrador para cargarlos de nuevo; devuelve los quitados (para borrarlos de la base).</summary>
    public IReadOnlyList<PurchaseItem> ClearItems()
    {
        if (Status != PurchaseStatus.Draft)
            throw new InvalidOperationException("Solo se pueden cambiar los ítems de una compra en borrador.");

        var removed = _items.ToList();
        _items.Clear();
        RecalculateTotals();
        return removed;
    }

    public void Confirm(Guid? accountId)
    {
        if (Status != PurchaseStatus.Draft)
            throw new InvalidOperationException("Solo se pueden confirmar compras en estado borrador.");
        if (_items.Count == 0)
            throw new InvalidOperationException("La compra debe tener al menos un ítem.");

        AccountId = accountId;
        Status = PurchaseStatus.Confirmed;
    }

    /// <summary>
    /// Anula la compra. Un borrador se descarta sin más; una confirmada exige motivo (el handler revierte el stock y la caja).
    /// </summary>
    public void Cancel(string? reason, DateTimeOffset at)
    {
        if (Status == PurchaseStatus.Cancelled)
            throw new InvalidOperationException("La compra ya está cancelada.");
        if (Status == PurchaseStatus.Confirmed && string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("El motivo de la anulación es obligatorio.", nameof(reason));

        Status = PurchaseStatus.Cancelled;
        CancelReason = reason?.Trim();
        CancelledAt = at;
    }

    public void SoftDelete(string? reason, Guid? deletedBy, DateTimeOffset deletedAt)
    {
        if (IsDeleted) return;
        DeletedAt = deletedAt;
        DeletedBy = deletedBy;
        DeleteReason = reason?.Trim();
    }

    private void RecalculateTotals()
    {
        SubtotalNet = _items.Sum(i => Math.Round(i.Quantity * i.UnitCost, 2, MidpointRounding.AwayFromZero));
        VatAmount = _items.Sum(i => Math.Round(i.Quantity * i.UnitCost * i.VatRate / 100m, 2, MidpointRounding.AwayFromZero));
        Total = SubtotalNet + VatAmount + Perceptions - Retentions;
    }
}
