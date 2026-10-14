using TallerControl.Domain.Common;

namespace TallerControl.Domain.Work;

/// <summary>
/// Renglón de una orden de trabajo. Los totales de línea se recalculan en <see cref="Recalculate"/>
/// (redondeo por renglón, MidpointRounding.AwayFromZero — doc 04 §5). No se borra en órdenes
/// entregadas (REQ-12).
/// </summary>
public sealed class WorkOrderItem : Entity, ITenantOwned
{
    private WorkOrderItem()
    {
    }

    public WorkOrderItem(
        Guid tenantId,
        Guid workOrderId,
        WorkOrderItemKind kind,
        string description,
        Guid? catalogItemId,
        decimal quantity,
        decimal unitNetPrice,
        decimal vatRate,
        decimal? unitCostSnapshot,
        int position,
        Guid? quoteItemId = null)
    {
        TenantId = tenantId;
        WorkOrderId = workOrderId;
        Kind = kind;
        Description = ValidateDescription(description);
        CatalogItemId = catalogItemId;
        Quantity = ValidateQuantity(quantity);
        UnitNetPrice = ValidateUnitNetPrice(unitNetPrice);
        VatRate = vatRate;
        UnitCostSnapshot = unitCostSnapshot;
        Position = position;
        QuoteItemId = quoteItemId;
        Recalculate();
    }

    public Guid TenantId { get; private set; }
    public Guid WorkOrderId { get; private set; }
    public WorkOrderItemKind Kind { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public Guid? CatalogItemId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitNetPrice { get; private set; }
    public decimal VatRate { get; private set; }
    public decimal LineNet { get; private set; }
    public decimal LineVat { get; private set; }
    public decimal LineTotal { get; private set; }
    public decimal? UnitCostSnapshot { get; private set; }
    public int Position { get; private set; }
    /// <summary>Origen en presupuesto; null para órdenes directas (se vincula en E6).</summary>
    public Guid? QuoteItemId { get; private set; }

    public void Update(string description, decimal quantity, decimal unitNetPrice, decimal vatRate, int position)
    {
        Description = ValidateDescription(description);
        Quantity = ValidateQuantity(quantity);
        UnitNetPrice = ValidateUnitNetPrice(unitNetPrice);
        VatRate = vatRate;
        Position = position;
        Recalculate();
    }

    private void Recalculate()
    {
        LineNet = decimal.Round(Quantity * UnitNetPrice, 2, MidpointRounding.AwayFromZero);
        LineVat = decimal.Round(LineNet * VatRate / 100m, 2, MidpointRounding.AwayFromZero);
        LineTotal = LineNet + LineVat;
    }

    private static string ValidateDescription(string description)
    {
        var trimmed = description?.Trim() ?? string.Empty;
        if (trimmed.Length is < 1 or > 200)
            throw new ArgumentException("La descripción debe tener entre 1 y 200 caracteres.", nameof(description));
        return trimmed;
    }

    private static decimal ValidateQuantity(decimal quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "La cantidad debe ser mayor a cero.");
        return quantity;
    }

    private static decimal ValidateUnitNetPrice(decimal price)
    {
        if (price < 0)
            throw new ArgumentOutOfRangeException(nameof(price), "El precio unitario neto no puede ser negativo.");
        return price;
    }
}
