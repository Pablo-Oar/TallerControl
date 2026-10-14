using System.Security.Cryptography;
using TallerControl.Application.Common;
using TallerControl.Application.PublicLinks;
using TallerControl.Domain.PublicLinks;

namespace TallerControl.Application.Work;

/// <summary>
/// Caso de uso: obtener (o renovar) el enlace público de una OT para compartir con el cliente (doc 05 §2.8).
/// Revoca el enlace activo anterior y genera uno nuevo; el token en texto plano solo viaja en esta respuesta.
/// </summary>
public sealed class GetWorkOrderPublicLinkHandler
{
    private readonly IWorkOrderRepository _woRepo;
    private readonly IPublicLinkRepository _linkRepo;
    private readonly ITenantContext _tenantContext;
    private readonly IActorContext _actorContext;
    private readonly TimeProvider _clock;

    public GetWorkOrderPublicLinkHandler(
        IWorkOrderRepository woRepo,
        IPublicLinkRepository linkRepo,
        ITenantContext tenantContext,
        IActorContext actorContext,
        TimeProvider clock)
    {
        _woRepo = woRepo;
        _linkRepo = linkRepo;
        _tenantContext = tenantContext;
        _actorContext = actorContext;
        _clock = clock;
    }

    public async Task<WorkOrderPublicLinkDto> HandleAsync(Guid workOrderId, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        _ = await _woRepo.FindByIdAsync(workOrderId, ct)
            ?? throw new NotFoundException("La orden de trabajo no existe.");

        var now = _clock.GetUtcNow();

        // Revocar todos los links activos anteriores para esta OT.
        var existing = await _linkRepo.FindActiveByTargetAsync(
            PublicLinkTargetType.WorkOrder, workOrderId, now, ct);

        foreach (var link in existing)
            link.Revoke(_actorContext.ActorId, now);

        // Generar nuevo token: 32 bytes aleatorios; solo el hash SHA-256 va a la base.
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var tokenHash = SHA256.HashData(tokenBytes);
        var expiresAt = now.AddDays(180);

        var newLink = new PublicLink(tenantId, PublicLinkTargetType.WorkOrder, workOrderId, tokenHash, expiresAt);
        await _linkRepo.AddAsync(newLink, ct);
        await _linkRepo.SaveChangesAsync(ct);

        var tokenBase64Url = Convert.ToBase64String(tokenBytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        return new WorkOrderPublicLinkDto(tokenBase64Url, expiresAt);
    }
}
