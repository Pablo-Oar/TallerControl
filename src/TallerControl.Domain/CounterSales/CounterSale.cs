using TallerControl.Domain.Common;

namespace TallerControl.Domain.CounterSales;

/// <summary>
/// Venta de mostrador: venta sin vehículo asociado (D-20).
/// No genera comprobante fiscal (E11-C); solo movimiento de caja.
/// </summary>
public sealed class CounterSale : Entity, ITenantOwned, IVersioned, IAuditable, ISoftDeletable
{
    private readonly List<CounterSaleItem> _items = [];

    private CounterSale()
    {
    }

    public CounterSale(
        Guid tenantId,
        Guid branchId,
        Guid? customerId,
        int documentNumber,
        DateTimeOffset date)
    {
        TenantId = tenantId;
        BranchId = branchId;
        CustomerId = customerId;
        DocumentNumber = documentNumber;
        Date = date;
        Status = CounterSaleStatus.Draft;
    }

    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public int DocumentNumber { get; private set; }
    public DateTimeOffset Date { get; private set; }
    public decimal SubtotalNet { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal Total { get; private set; }
    public CounterSaleStatus Status { get; private set; }

    /// <summary>Motivo y momento de la devolución/anulación (una venta confirmada se devuelve con contraasientos).</summary>
    public string? CancelReason { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public Guid? AccountId { get; private set; }
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

    public IReadOnlyList<CounterSaleItem> Items => _items.AsReadOnly();

    public void AddItem(CounterSaleItem item)
    {
        if (Status != CounterSaleStatus.Draft)
            throw new InvalidOperationException("Solo se pueden agregar ítems a una venta en borrador.");
        _items.Add(item);
        RecalculateTotals();
    }

    public void Confirm(Guid? accountId)
    {
        if (Status != CounterSaleStatus.Draft)
            throw new InvalidOperationException("Solo se pueden confirmar ventas en estado borrador.");
        if (_items.Count == 0)
            throw new InvalidOperationException("La venta debe tener al menos un ítem.");

        AccountId = accountId;
        Status = CounterSaleStatus.Confirmed;
    }

    /// <summary>
    /// Cancela un borrador o devuelve una venta confirmada (con motivo obligatorio): el handler repone el stock,
    /// deja el contraasiento de caja y emite la nota de crédito si estaba facturada.
    /// </summary>
    public void Cancel(string? reason, DateTimeOffset at)
    {
        if (Status == CounterSaleStatus.Cancelled)
            throw new InvalidOperationException("La venta ya está cancelada.");
        if (Status == CounterSaleStatus.Confirmed && string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("El motivo de la devolución es obligatorio.", nameof(reason));

        Status = CounterSaleStatus.Cancelled;
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
        SubtotalNet = _items.Sum(i => i.NetAmount);
        VatAmount = _items.Sum(i => i.VatAmount);
        Total = SubtotalNet + VatAmount;
    }
}
