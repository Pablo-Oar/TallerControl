using System.Net;
using TallerControl.Domain.Common;

namespace TallerControl.Domain.Quotes;

/// <summary>
/// Respuesta del cliente a un presupuesto. Solo inserción; la base revoca UPDATE/DELETE
/// para el rol de aplicación (doc 04 §3.5, REQ-10). El campo <see cref="IsCurrent"/> tiene
/// un índice único parcial en la base.
/// </summary>
public sealed class QuoteResponse : Entity, ITenantOwned
{
    public const int SignerNameMaxLength = 150;
    public const int CommentMaxLength = 1000;

    private readonly List<QuoteResponseItem> _items = [];

    private QuoteResponse()
    {
    }

    public QuoteResponse(
        Guid tenantId,
        Guid quoteId,
        string decision,
        string signerName,
        decimal approvedTotal,
        byte[] contentHash,
        IPAddress? clientIpTruncated,
        string? userAgent,
        string? comment,
        DateTimeOffset createdAt,
        IEnumerable<QuoteResponseItem> items)
    {
        TenantId = tenantId;
        QuoteId = quoteId;
        Decision = decision;
        SignerName = signerName?.Trim()
            ?? throw new ArgumentNullException(nameof(signerName));
        if (SignerName.Length is < 1 or > SignerNameMaxLength)
            throw new ArgumentException($"El nombre del firmante debe tener entre 1 y {SignerNameMaxLength} caracteres.", nameof(signerName));
        ApprovedTotal = approvedTotal;
        ContentHash = contentHash ?? throw new ArgumentNullException(nameof(contentHash));
        ClientIpTruncated = clientIpTruncated;
        UserAgent = userAgent;
        Comment = comment?.Trim() is { Length: > 0 } c ? c : null;
        IsCurrent = true;
        CreatedAt = createdAt;
        _items.AddRange(items);
    }

    public Guid TenantId { get; private set; }
    public Guid QuoteId { get; private set; }
    public string Decision { get; private set; } = string.Empty;
    public string SignerName { get; private set; } = string.Empty;
    public decimal ApprovedTotal { get; private set; }
    public byte[] ContentHash { get; private set; } = [];
    public IPAddress? ClientIpTruncated { get; private set; }
    public string? UserAgent { get; private set; }
    public string? Comment { get; private set; }
    public bool IsCurrent { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<QuoteResponseItem> Items => _items.AsReadOnly();

    /// <summary>Marca esta respuesta como histórica cuando llega una nueva.</summary>
    public void Supersede() => IsCurrent = false;
}
