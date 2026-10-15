namespace TallerControl.Domain.Tenancy;

/// <summary>Tipos de documento con numeración correlativa propia por sucursal.</summary>
public enum DocumentKind
{
    Quote,
    WorkOrder,
    Payment,
    Purchase,
    CounterSale,
    CashSession,
}
