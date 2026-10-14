namespace TallerControl.Domain.Quotes;

/// <summary>
/// Decisión por renglón dentro de una respuesta del cliente (doc 04 §3.5).
/// PK compuesta (response_id, quote_item_id) — sin herencia de Entity.
/// </summary>
public sealed class QuoteResponseItem
{
    private QuoteResponseItem()
    {
    }

    public QuoteResponseItem(Guid responseId, Guid quoteItemId, bool approved)
    {
        ResponseId = responseId;
        QuoteItemId = quoteItemId;
        Approved = approved;
    }

    public Guid ResponseId { get; private set; }
    public Guid QuoteItemId { get; private set; }
    public bool Approved { get; private set; }
}
