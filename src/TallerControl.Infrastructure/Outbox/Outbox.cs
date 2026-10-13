using System.Text.Json;
using TallerControl.Application.Common;
using TallerControl.Domain.Outbox;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Outbox;

/// <summary>
/// Agrega el mensaje al contexto actual: se persiste con el próximo <c>SaveChanges</c>, en la misma
/// transacción que el cambio de negocio.
/// </summary>
public sealed class OutboxWriter : IOutbox
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _clock;
    private readonly ITenantContext _tenant;

    public OutboxWriter(AppDbContext db, TimeProvider clock, ITenantContext tenant)
    {
        _db = db;
        _clock = clock;
        _tenant = tenant;
    }

    public void Enqueue(string type, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        _db.Set<OutboxMessage>().Add(new OutboxMessage(type, json, _clock.GetUtcNow(), _tenant.TenantId));
    }
}
