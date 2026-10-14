using TallerControl.Domain.Common;

namespace TallerControl.Domain.Quotes;

/// <summary>
/// Presupuesto de un trabajo. Ciclo de vida: Draft → Sent → Approved / PartiallyApproved /
/// Rejected / Expired → Converted. Cancelado desde cualquier estado previo a Converted.
/// Borrado lógico solo desde Draft (REQ-09, doc 04 §3.5).
/// Los totales se calculan en el servidor; DiscountAmount sale de GlobalDiscountPct (doc 04 §5).
/// </summary>
public sealed class Quote : Entity, ITenantOwned, IVersioned, IAudited, IAuditable, ISoftDeletable
{
    public const int CustomerNotesMaxLength = 2000;
    public const int InternalNotesMaxLength = 2000;
    public const int CancelReasonMaxLength = 500;
    public const int ReopenReasonMaxLength = 500;

    // Decisión del cliente en QuoteResponse.Decision
    public const string DecisionApproved = "approved";
    public const string DecisionRejected = "rejected";

    private readonly List<QuoteItem> _items = [];
    private readonly List<QuoteResponse> _responses = [];

    private Quote()
    {
    }

    public Quote(
        Guid tenantId,
        int number,
        Guid customerId,
        Guid vehicleId,
        DateOnly validUntil,
        string? customerNotes,
        string? internalNotes,
        Guid? createdBy)
    {
        TenantId = tenantId;
        Number = number;
        CustomerId = customerId;
        VehicleId = vehicleId;
        ValidUntil = validUntil;
        CustomerNotes = customerNotes?.Trim();
        InternalNotes = internalNotes?.Trim();
        Status = QuoteStatus.Draft;
    }

    public Guid TenantId { get; private set; }
    public int Number { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid VehicleId { get; private set; }
    public QuoteStatus Status { get; private set; }
    public DateOnly ValidUntil { get; private set; }
    public decimal GlobalDiscountPct { get; private set; }
    public decimal SubtotalNet { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal VatAmount { get; private set; }
    public decimal Total { get; private set; }
    public decimal? ApprovedTotal { get; private set; }
    public string? CustomerNotes { get; private set; }
    public string? InternalNotes { get; private set; }
    public Guid? WorkOrderId { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public DateTimeOffset? ConvertedAt { get; private set; }
    public string? CancelReason { get; private set; }
    public uint Version { get; private set; }

    // IAuditable
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    // ISoftDeletable — solo aplicable en Draft
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }
    public string? DeleteReason { get; private set; }

    public bool IsDeleted => DeletedAt.HasValue;

    public IReadOnlyList<QuoteItem> Items => _items.AsReadOnly();
    public IReadOnlyList<QuoteResponse> Responses => _responses.AsReadOnly();

    // -----------------------------------------------------------------------
    // Operaciones del ciclo de vida
    // -----------------------------------------------------------------------

    /// <summary>Actualiza encabezado y renglones (solo desde Draft).</summary>
    public void Update(
        Guid customerId,
        Guid vehicleId,
        DateOnly validUntil,
        decimal globalDiscountPct,
        string? customerNotes,
        string? internalNotes,
        IEnumerable<QuoteItem> items)
    {
        GuardDraft();
        CustomerId = customerId;
        VehicleId = vehicleId;
        ValidUntil = validUntil;
        GlobalDiscountPct = ValidateDiscountPct(globalDiscountPct);
        CustomerNotes = customerNotes?.Trim();
        InternalNotes = internalNotes?.Trim();

        _items.Clear();
        _items.AddRange(items);
        RecalculateTotals();
    }

    /// <summary>Agrega un renglón y recalcula (solo desde Draft).</summary>
    public QuoteItem AddItem(
        QuoteItemKind kind,
        string description,
        Guid? catalogItemId,
        decimal quantity,
        decimal unitNetPrice,
        decimal vatRate,
        decimal? unitCostSnapshot,
        bool requiredForApproval = false)
    {
        GuardDraft();
        var position = _items.Count > 0 ? _items.Max(i => i.Position) + 1 : 1;
        var item = new QuoteItem(TenantId, Id, kind, description, catalogItemId,
            quantity, unitNetPrice, vatRate, unitCostSnapshot, position, requiredForApproval);
        _items.Add(item);
        RecalculateTotals();
        return item;
    }

    /// <summary>Marca el presupuesto como Enviado y devuelve el timestamp.</summary>
    public DateTimeOffset Send(DateTimeOffset sentAt)
    {
        if (Status != QuoteStatus.Draft)
            throw new InvalidOperationException($"Solo se puede enviar un presupuesto en borrador (estado actual: {Status}).");
        if (_items.Count == 0)
            throw new InvalidOperationException("No se puede enviar un presupuesto sin renglones.");

        Status = QuoteStatus.Sent;
        SentAt = sentAt;
        return sentAt;
    }

    /// <summary>
    /// Vuelve a Draft desde Sent o Rejected (invalida las respuestas vigentes).
    /// Retorna la lista de respuestas que se marcaron como no vigentes.
    /// </summary>
    public IReadOnlyList<QuoteResponse> Reopen(string? reason)
    {
        if (Status is not (QuoteStatus.Sent or QuoteStatus.Rejected))
            throw new InvalidOperationException(
                $"Solo se puede reabrir un presupuesto enviado o rechazado (estado actual: {Status}).");

        Status = QuoteStatus.Draft;
        SentAt = null;
        ApprovedTotal = null;

        var superseded = _responses.Where(r => r.IsCurrent).ToList();
        foreach (var r in superseded) r.Supersede();
        return superseded;
    }

    /// <summary>
    /// Registra la respuesta del cliente. Actualiza el estado del presupuesto y el
    /// ApprovedTotal según los ítems aprobados. Invalida la respuesta anterior si existe.
    /// Retorna la respuesta previa si había (para el historial).
    /// </summary>
    public QuoteResponse RecordResponse(QuoteResponse response)
    {
        if (Status is not (QuoteStatus.Sent or QuoteStatus.Approved or QuoteStatus.PartiallyApproved))
            throw new InvalidOperationException(
                $"No se puede registrar una respuesta en estado {Status}.");

        // Invalida la vigente anterior
        var prev = _responses.FirstOrDefault(r => r.IsCurrent);
        prev?.Supersede();

        _responses.Add(response);

        var approvedItemIds = response.Items
            .Where(ri => ri.Approved)
            .Select(ri => ri.QuoteItemId)
            .ToHashSet();

        var approvedItems = _items.Where(i => approvedItemIds.Contains(i.Id)).ToList();

        if (response.Decision == DecisionRejected || approvedItems.Count == 0)
        {
            Status = QuoteStatus.Rejected;
            ApprovedTotal = null;
        }
        else if (approvedItems.Count == _items.Count)
        {
            Status = QuoteStatus.Approved;
            ApprovedTotal = response.ApprovedTotal;
        }
        else
        {
            Status = QuoteStatus.PartiallyApproved;
            ApprovedTotal = response.ApprovedTotal;
        }

        return response;
    }

    /// <summary>
    /// Verifica si el presupuesto venció (ValidUntil &lt; today) y lo marca como Expired.
    /// Solo aplica en Sent.
    /// </summary>
    public bool CheckExpiry(DateOnly today)
    {
        if (Status != QuoteStatus.Sent) return false;
        if (ValidUntil >= today) return false;

        Status = QuoteStatus.Expired;
        return true;
    }

    /// <summary>
    /// Marca el presupuesto como Convertido y vincula la orden creada.
    /// Solo desde Approved o PartiallyApproved (REQ-11).
    /// </summary>
    public void MarkConverted(Guid workOrderId, DateTimeOffset convertedAt)
    {
        if (Status is not (QuoteStatus.Approved or QuoteStatus.PartiallyApproved))
            throw new InvalidOperationException(
                $"Solo se pueden convertir presupuestos aprobados (estado actual: {Status}).");

        if (WorkOrderId.HasValue)
            throw new InvalidOperationException("Este presupuesto ya fue convertido.");

        Status = QuoteStatus.Converted;
        WorkOrderId = workOrderId;
        ConvertedAt = convertedAt;
    }

    /// <summary>Cancela el presupuesto desde cualquier estado no terminal.</summary>
    public void Cancel(string reason)
    {
        if (Status is QuoteStatus.Converted or QuoteStatus.Cancelled)
            throw new InvalidOperationException(
                $"No se puede cancelar un presupuesto en estado {Status}.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("El motivo de cancelación es obligatorio.", nameof(reason));

        Status = QuoteStatus.Cancelled;
        CancelReason = reason.Trim();
    }

    /// <summary>Borrado lógico; solo en Draft.</summary>
    public void Delete(string? reason, Guid? deletedBy, DateTimeOffset deletedAt)
    {
        if (Status != QuoteStatus.Draft)
            throw new InvalidOperationException("Solo se pueden borrar presupuestos en borrador.");

        if (IsDeleted) return;
        DeletedAt = deletedAt;
        DeletedBy = deletedBy;
        DeleteReason = reason?.Trim();
    }

    // -----------------------------------------------------------------------
    // Cálculo de totales
    // -----------------------------------------------------------------------

    private void RecalculateTotals()
    {
        SubtotalNet = _items.Sum(i => i.LineNet);
        DiscountAmount = decimal.Round(SubtotalNet * GlobalDiscountPct / 100m, 2, MidpointRounding.AwayFromZero);
        VatAmount = _items.Sum(i =>
            decimal.Round(i.LineNet * (1m - GlobalDiscountPct / 100m) * i.VatRate / 100m, 2, MidpointRounding.AwayFromZero));
        Total = SubtotalNet - DiscountAmount + VatAmount;
    }

    private void GuardDraft()
    {
        if (Status != QuoteStatus.Draft)
            throw new InvalidOperationException(
                $"Solo se pueden modificar renglones de un presupuesto en borrador (estado actual: {Status}).");
    }

    private static decimal ValidateDiscountPct(decimal pct)
    {
        if (pct is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(pct), "El descuento global debe estar entre 0 y 100 %.");
        return pct;
    }
}
