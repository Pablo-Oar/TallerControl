namespace TallerControl.Domain.Catalog;

/// <summary>Motivo de un movimiento de stock (doc 04 §3.3). purchase es P1.</summary>
public enum StockMovementReason
{
    WorkOrder,
    WorkOrderCancel,
    ItemRemoved,
    ManualAdjust,
    Purchase,
    PurchaseCancel,
    CounterSale,
    CounterSaleCancel
}
