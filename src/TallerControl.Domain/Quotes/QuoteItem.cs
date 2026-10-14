using TallerControl.Domain.Common;

namespace TallerControl.Domain.Quotes;

/// <summary>
/// Renglón de un presupuesto. Admite ítems del catálogo o libres.
/// Los totales se recalculan por renglón con redondeo AwayFromZero (doc 04 §5, REQ-09).
/// </summary>
public sealed class QuoteItem : Entity, ITenantOwned
{
    public const int DescriptionMaxLength = 200;

    private QuoteItem()
    {
    }

    public QuoteItem(
        Guid tenantId,
        Guid quoteId,
        QuoteItemKind kind,
        string description,
        Guid? catalogItemId,
        decimal quantity,
        decimal unitNetPrice,
        decimal vatRate,
        decimal? unitCostSnapshot,
        int position,
        bool requiredForApproval = false)
    {
        TenantId = tenantId;
        QuoteId = quoteId;
        Kind = kind;
        Description = ValidateDescription(description);
        CatalogItemId = catalogItemId;
        Quantity = ValidateQuantity(quantity);
        UnitNetPrice = ValidateUnitNetPrice(unitNetPrice);
        VatRate = vatRate;
        UnitCostSnapshot = unitCostSnapshot;
        Position = position;
        RequiredForApproval = requiredForApproval;
        Recalculate();
    }

    public Guid TenantId { get; private set; }
    public Guid QuoteId { get; private set; }
    public QuoteItemKind Kind { get; private set; }
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
    public bool RequiredForApproval { get; private set; }

    public void Update(
        string description,
        decimal quantity,
        decimal unitNetPrice,
        decimal vatRate,
        int position,
        bool requiredForApproval)
    {
        Description = ValidateDescription(description);
        Quantity = ValidateQuantity(quantity);
        UnitNetPrice = ValidateUnitNetPrice(unitNetPrice);
        VatRate = vatRate;
        Position = position;
        RequiredForApproval = requiredForApproval;
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
        if (trimmed.Length is < 1 or > DescriptionMaxLength)
            throw new ArgumentException(
                $"La descripción debe tener entre 1 y {DescriptionMaxLength} caracteres.", nameof(description));
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
