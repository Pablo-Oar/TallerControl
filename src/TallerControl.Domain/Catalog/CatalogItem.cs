using TallerControl.Domain.Common;

namespace TallerControl.Domain.Catalog;

/// <summary>
/// Servicio o producto del catálogo del taller. El stock solo se modifica a través de
/// <see cref="StockMovement"/>: nunca asignar <see cref="StockQty"/> directamente (REQ-08, doc 04 §3.3).
/// </summary>
public sealed class CatalogItem : Entity, ITenantOwned, IVersioned, IAudited, IAuditable, ISoftDeletable
{
    public const int NameMaxLength = 200;
    public const int SkuMaxLength = 50;

    private CatalogItem()
    {
    }

    public CatalogItem(
        Guid tenantId,
        CatalogItemKind kind,
        string name,
        string? sku,
        decimal netPrice,
        decimal vatRate,
        decimal? cost,
        bool trackStock,
        decimal? stockMin,
        int? durationMinutes,
        int? bufferMinutes)
    {
        TenantId = tenantId;
        Kind = kind;
        Name = ValidateName(name);
        Sku = NormalizeSku(sku);
        NetPrice = ValidateNetPrice(netPrice);
        VatRate = vatRate;
        Cost = cost;
        TrackStock = ValidateTrackStock(trackStock, kind);
        StockQty = 0;
        StockMin = stockMin;
        DurationMinutes = durationMinutes;
        BufferMinutes = bufferMinutes;
        IsActive = true;
    }

    public Guid TenantId { get; private set; }
    public CatalogItemKind Kind { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Sku { get; private set; }
    public decimal NetPrice { get; private set; }
    public decimal VatRate { get; private set; }
    public decimal? Cost { get; private set; }
    public bool TrackStock { get; private set; }
    public decimal StockQty { get; private set; }
    public decimal? StockMin { get; private set; }
    public int? DurationMinutes { get; private set; }
    public int? BufferMinutes { get; private set; }
    public bool IsActive { get; private set; }
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
    /// Actualiza los metadatos del ítem. El stock <strong>no</strong> se toca aquí (T-CAT-03).
    /// Cambiar el precio no afecta renglones ya cargados en órdenes/presupuestos (T-CAT-04).
    /// </summary>
    public void Update(
        string name,
        string? sku,
        decimal netPrice,
        decimal vatRate,
        decimal? cost,
        decimal? stockMin,
        int? durationMinutes,
        int? bufferMinutes,
        bool isActive)
    {
        Name = ValidateName(name);
        Sku = NormalizeSku(sku);
        NetPrice = ValidateNetPrice(netPrice);
        VatRate = vatRate;
        Cost = cost;
        StockMin = stockMin;
        DurationMinutes = durationMinutes;
        BufferMinutes = bufferMinutes;
        IsActive = isActive;
    }

    /// <summary>
    /// Aplica un movimiento de stock. Solo llamar desde el handler de ajuste, en la misma transacción
    /// que persiste el <see cref="StockMovement"/> (D-07: stock negativo permitido con alerta).
    /// </summary>
    public void ApplyStockMovement(decimal quantity)
    {
        if (quantity == 0)
            throw new ArgumentException("La cantidad del movimiento no puede ser cero.", nameof(quantity));

        if (!TrackStock)
            throw new InvalidOperationException(
                "Solo los productos con seguimiento de stock admiten movimientos.");

        StockQty += quantity;
    }

    /// <summary>
    /// ¿El stock está por debajo del mínimo? (D-07: alerta de stock negativo o bajo mínimo).
    /// </summary>
    /// <summary>Stock bajo: por debajo del mínimo configurado, o negativo (D-07: se permite vender sin stock, con alerta).</summary>
    public bool IsLowStock => TrackStock && (StockQty < 0 || (StockMin.HasValue && StockQty < StockMin.Value));

    public void SoftDelete(string? reason, Guid? deletedBy, DateTimeOffset deletedAt)
    {
        if (IsDeleted) return;
        DeletedAt = deletedAt;
        DeletedBy = deletedBy;
        DeleteReason = reason?.Trim();
    }

    public void Restore()
    {
        DeletedAt = null;
        DeletedBy = null;
        DeleteReason = null;
    }

    private static string ValidateName(string name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length is < 1 or > NameMaxLength)
            throw new ArgumentException($"El nombre debe tener entre 1 y {NameMaxLength} caracteres.", nameof(name));
        return trimmed;
    }

    private static string? NormalizeSku(string? sku) =>
        string.IsNullOrWhiteSpace(sku) ? null : sku.Trim().ToUpperInvariant();

    private static decimal ValidateNetPrice(decimal netPrice)
    {
        if (netPrice < 0)
            throw new ArgumentOutOfRangeException(nameof(netPrice), "El precio neto no puede ser negativo.");
        return netPrice;
    }

    private static bool ValidateTrackStock(bool trackStock, CatalogItemKind kind)
    {
        if (trackStock && kind != CatalogItemKind.Product)
            throw new ArgumentException(
                "Solo los productos pueden tener seguimiento de stock.", nameof(trackStock));
        return trackStock;
    }
}
