using TallerControl.Application.Common;
using TallerControl.Application.Work;
using TallerControl.Domain.PublicLinks;

namespace TallerControl.Application.PublicLinks;

/// <summary>
/// Caso de uso: obtener la vista pública de una orden de trabajo por token (doc 05 §2.8).
/// El token apunta a un enlace de tipo WorkOrder; la respuesta no incluye precios.
/// </summary>
public sealed class GetPublicWorkOrderHandler
{
    private readonly IPublicLinkRepository _linkRepo;
    private readonly IWorkOrderRepository _woRepo;
    private readonly TimeProvider _clock;
    private readonly IWorkshopBranding _branding;

    public GetPublicWorkOrderHandler(
        IPublicLinkRepository linkRepo,
        IWorkOrderRepository woRepo,
        TimeProvider clock,
        IWorkshopBranding branding)
    {
        _linkRepo = linkRepo;
        _woRepo = woRepo;
        _clock = clock;
        _branding = branding;
    }

    public async Task<PublicWorkOrderDto> HandleAsync(string tokenBase64Url, CancellationToken ct)
    {
        var tokenHash = GetPublicQuoteHandler.HashToken(tokenBase64Url);

        var now = _clock.GetUtcNow();
        var link = await _linkRepo.FindByTokenHashAsync(tokenHash, ct);

        if (link == null || !link.IsActive(now) || link.TargetType != PublicLinkTargetType.WorkOrder)
            throw new NotFoundException();

        // includePrices: false — los precios no viajan en la vista pública (REQ-14, doc 07 §3).
        var order = await _woRepo.GetFullAsync(link.TargetId, includePrices: false, ct)
            ?? throw new NotFoundException();

        link.RecordAccess(now);
        await _linkRepo.SaveChangesAsync(ct);

        var shop = await _branding.GetPublicAsync(link.TenantId, ct);

        return new PublicWorkOrderDto(
            order.Id,
            order.Number,
            order.Status.ToSnake(),
            order.CustomerName,
            order.VehiclePlate,
            order.VehicleBrand,
            order.VehicleModel,
            order.AssignedMechanicName,
            order.ReportedIssue,
            order.MechanicNotes,
            order.CreatedAt,
            shop.Name,
            shop.LogoUrl);
    }
}
