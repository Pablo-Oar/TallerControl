using TallerControl.Domain.Common;

namespace TallerControl.Domain.Purchases;

public sealed class PurchaseItem : Entity, ITenantOwned
{
    public const int DescriptionMaxLength = 300;

    private PurchaseItem()
    {
    }

    public PurchaseItem(
        Guid tenantId,
        Guid purchaseId,
        Guid? catalogItemId,
        string description,
        decimal quantity,
        decimal unitCost,
        decimal vatRate)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "La cantidad debe ser mayor que cero.");
        if (unitCost < 0)
            throw new ArgumentOutOfRangeException(nameof(unitCost), "El costo unitario no puede ser negativo.");
        if (vatRate < 0 || vatRate > 1)
            throw new ArgumentOutOfRangeException(nameof(vatRate), "La tasa de IVA debe estar entre 0 y 1.");

        TenantId = tenantId;
        PurchaseId = purchaseId;
        CatalogItemId = catalogItemId;
        Description = description?.Trim() ?? string.Empty;
        Quantity = quantity;
        UnitCost = unitCost;
        VatRate = vatRate;
        Total = Math.Round(quantity * unitCost * (1 + vatRate), 2, MidpointRounding.AwayFromZero);
    }

    public Guid TenantId { get; private set; }
    public Guid PurchaseId { get; private set; }
    public Guid? CatalogItemId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal VatRate { get; private set; }
    public decimal Total { get; private set; }
}
