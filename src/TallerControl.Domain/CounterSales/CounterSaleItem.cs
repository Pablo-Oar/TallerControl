using TallerControl.Domain.Common;

namespace TallerControl.Domain.CounterSales;

public sealed class CounterSaleItem : Entity, ITenantOwned
{
    public const int DescriptionMaxLength = 300;

    private CounterSaleItem()
    {
    }

    public CounterSaleItem(
        Guid tenantId,
        Guid counterSaleId,
        Guid? catalogItemId,
        string description,
        decimal quantity,
        decimal unitPrice,
        decimal vatRate)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "La cantidad debe ser mayor que cero.");
        if (unitPrice < 0)
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "El precio unitario no puede ser negativo.");
        if (vatRate < 0 || vatRate > 100)
            throw new ArgumentOutOfRangeException(nameof(vatRate), "La tasa de IVA debe estar entre 0 y 100.");

        TenantId = tenantId;
        CounterSaleId = counterSaleId;
        CatalogItemId = catalogItemId;
        Description = description?.Trim() ?? string.Empty;
        Quantity = quantity;
        UnitPrice = unitPrice;
        VatRate = vatRate;
        NetAmount = Math.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
        VatAmount = Math.Round(NetAmount * vatRate / 100m, 2, MidpointRounding.AwayFromZero);
        Total = NetAmount + VatAmount;
    }

    public Guid TenantId { get; private set; }
    public Guid CounterSaleId { get; private set; }
    public Guid? CatalogItemId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal VatRate { get; private set; }
    public decimal NetAmount { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal Total { get; private set; }
}
