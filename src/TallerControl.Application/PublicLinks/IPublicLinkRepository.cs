using TallerControl.Domain.PublicLinks;

namespace TallerControl.Application.PublicLinks;

public interface IPublicLinkRepository
{
    /// <summary>Busca un enlace por su hash de token (búsqueda anónima, sin filtro de taller).</summary>
    Task<PublicLink?> FindByTokenHashAsync(byte[] tokenHash, CancellationToken ct);

    /// <summary>Busca todos los enlaces activos de un target (aplica filtro de taller).</summary>
    Task<IReadOnlyList<PublicLink>> FindActiveByTargetAsync(
        PublicLinkTargetType targetType, Guid targetId, DateTimeOffset now, CancellationToken ct);

    Task AddAsync(PublicLink link, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
