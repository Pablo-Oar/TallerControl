using TallerControl.Domain.Work;

namespace TallerControl.Application.Work;

public interface IAttachmentRepository
{
    Task<int> CountReadyAsync(AttachmentOwnerType ownerType, Guid ownerId, CancellationToken ct);

    Task<Attachment?> FindByIdAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Attachment>> ListAsync(AttachmentOwnerType ownerType, Guid ownerId, CancellationToken ct);

    Task AddAsync(Attachment attachment, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
