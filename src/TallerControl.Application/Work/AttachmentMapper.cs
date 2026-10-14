using TallerControl.Application.Common;
using TallerControl.Domain.Work;

namespace TallerControl.Application.Work;

internal static class AttachmentMapper
{
    private static readonly TimeSpan DefaultGetExpiry = TimeSpan.FromMinutes(10);

    public static AttachmentDto ToDto(Attachment a) =>
        new(a.Id, a.OwnerType, a.OwnerId, a.ContentType, a.SizeBytes,
            a.IsInternal, a.Status, DownloadUrl: null, a.CreatedBy, a.CreatedAt);

    public static AttachmentDto ToDtoWithUrl(Attachment a, IObjectStorage storage, TimeSpan expiry) =>
        new(a.Id, a.OwnerType, a.OwnerId, a.ContentType, a.SizeBytes,
            a.IsInternal, a.Status,
            DownloadUrl: a.Status == AttachmentStatus.Ready ? storage.CreatePresignedGetUrl(a.FileKey, expiry) : null,
            a.CreatedBy, a.CreatedAt);
}
