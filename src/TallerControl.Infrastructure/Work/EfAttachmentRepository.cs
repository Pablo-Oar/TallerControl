using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Work;
using TallerControl.Domain.Work;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Work;

internal sealed class EfAttachmentRepository : IAttachmentRepository
{
    private readonly AppDbContext _db;

    public EfAttachmentRepository(AppDbContext db) => _db = db;

    public Task<int> CountReadyAsync(AttachmentOwnerType ownerType, Guid ownerId, CancellationToken ct) =>
        _db.Set<Attachment>()
            .CountAsync(a => a.OwnerType == ownerType && a.OwnerId == ownerId
                && a.Status == AttachmentStatus.Ready && a.DeletedAt == null, ct);

    public Task<Attachment?> FindByIdAsync(Guid id, CancellationToken ct) =>
        _db.Set<Attachment>()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Attachment>> ListAsync(AttachmentOwnerType ownerType, Guid ownerId, CancellationToken ct) =>
        await _db.Set<Attachment>()
            .AsNoTracking()
            .Where(a => a.OwnerType == ownerType && a.OwnerId == ownerId
                && a.Status == AttachmentStatus.Ready && a.DeletedAt == null)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(Attachment attachment, CancellationToken ct) =>
        await _db.Set<Attachment>().AddAsync(attachment, ct);

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
