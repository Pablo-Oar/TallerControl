using TallerControl.Domain.Common;

namespace TallerControl.Domain.Outbox;

/// <summary>
/// Efecto externo pendiente (email, por ejemplo). Se escribe en la misma transacción que el cambio
/// de negocio y lo despacha un proceso aparte, así un fallo del envío no revierte el negocio.
/// </summary>
public sealed class OutboxMessage : Entity, IOptionalTenantOwned
{
    private OutboxMessage()
    {
    }

    public OutboxMessage(string type, string payloadJson, DateTimeOffset createdAt, Guid? tenantId = null)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("El mensaje necesita un tipo.", nameof(type));
        }

        Type = type;
        PayloadJson = payloadJson;
        CreatedAt = createdAt;
        TenantId = tenantId;
    }

    public Guid? TenantId { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string PayloadJson { get; private set; } = "{}";

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public void MarkProcessed(DateTimeOffset at)
    {
        ProcessedAt = at;
        LastError = null;
    }

    public void RegisterFailure(string error)
    {
        Attempts++;
        LastError = error;
    }
}
