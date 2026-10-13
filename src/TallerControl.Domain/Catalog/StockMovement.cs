using TallerControl.Domain.Common;

namespace TallerControl.Domain.Catalog;

/// <summary>
/// Movimiento de stock: solo inserción. El stock de <see cref="CatalogItem"/> es la suma de sus
/// movimientos (doc 04 §3.3, T-STK-02). La base revoca UPDATE/DELETE para el rol de aplicación.
/// </summary>
public sealed class StockMovement : Entity, ITenantOwned
{
    private StockMovement()
    {
    }

    public StockMovement(
        Guid tenantId,
        Guid branchId,
        Guid catalogItemId,
        decimal quantity,
        StockMovementReason reason,
        string? note,
        Guid? workOrderItemId,
        Guid? createdBy,
        DateTimeOffset createdAt)
    {
        if (quantity == 0)
            throw new ArgumentException("La cantidad no puede ser cero.", nameof(quantity));

        if (reason == StockMovementReason.ManualAdjust && string.IsNullOrWhiteSpace(note))
            throw new ArgumentException("La nota es obligatoria para ajustes manuales.", nameof(note));

        TenantId = tenantId;
        BranchId = branchId;
        CatalogItemId = catalogItemId;
        Quantity = quantity;
        Reason = reason;
        Note = note?.Trim();
        WorkOrderItemId = workOrderItemId;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid CatalogItemId { get; private set; }
    public decimal Quantity { get; private set; }
    public StockMovementReason Reason { get; private set; }
    public string? Note { get; private set; }
    public Guid? WorkOrderItemId { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
