using TallerControl.Application.Common;
using TallerControl.Application.Tenancy;
using TallerControl.Application.Vehicles;
using TallerControl.Domain.Quotes;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Application.Quotes;

/// <summary>
/// Caso de uso: crear un presupuesto en borrador (doc 05 §2.6, REQ-09, T-QUO-02).
/// La numeración correlativa exige una transacción activa (SELECT FOR UPDATE).
/// </summary>
public sealed class CreateQuoteHandler
{
    private readonly IQuoteRepository _repo;
    private readonly IVehicleRepository _vehicleRepo;
    private readonly ITenantContext _tenantContext;
    private readonly IActorContext _actorContext;
    private readonly IDocumentNumberGenerator _numbers;
    private readonly ITenantSettingsReader _settingsReader;
    private readonly TimeProvider _clock;

    public CreateQuoteHandler(
        IQuoteRepository repo,
        IVehicleRepository vehicleRepo,
        ITenantContext tenantContext,
        IActorContext actorContext,
        IDocumentNumberGenerator numbers,
        ITenantSettingsReader settingsReader,
        TimeProvider clock)
    {
        _repo = repo;
        _vehicleRepo = vehicleRepo;
        _tenantContext = tenantContext;
        _actorContext = actorContext;
        _numbers = numbers;
        _settingsReader = settingsReader;
        _clock = clock;
    }

    public async Task<QuoteDto> HandleAsync(CreateQuoteRequest request, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        var vehicle = await _vehicleRepo.FindByIdAsync(request.VehicleId, ct)
            ?? throw new NotFoundException("El vehículo no existe.");

        if (vehicle.CustomerId != request.CustomerId)
            throw new BusinessRuleException("El vehículo no pertenece al cliente indicado.");

        var now = _clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.DateTime);
        var actorId = _actorContext.ActorId;

        DateOnly validUntil;
        if (request.ValidUntil.HasValue)
        {
            validUntil = request.ValidUntil.Value;
            if (validUntil <= today)
                throw new BusinessRuleException("La vigencia del presupuesto debe ser una fecha futura.");
        }
        else
        {
            var settings = await _settingsReader.GetAsync(tenantId, ct);
            validUntil = today.AddDays(settings.QuoteValidityDays);
        }

        await using var tx = await _repo.BeginTransactionAsync(ct);
        var number = await _numbers.NextAsync(DocumentKind.Quote, ct);

        Quote quote;
        try
        {
            quote = new Quote(
                tenantId, number,
                request.CustomerId, request.VehicleId,
                validUntil,
                request.CustomerNotes,
                request.InternalNotes,
                actorId);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        if (request.Items != null)
        {
            foreach (var itemReq in request.Items)
            {
                try
                {
                    quote.AddItem(
                        itemReq.Kind,
                        itemReq.Description,
                        itemReq.CatalogItemId,
                        itemReq.Quantity,
                        itemReq.UnitNetPrice,
                        itemReq.VatRate,
                        unitCostSnapshot: null,
                        itemReq.RequiredForApproval);
                }
                catch (ArgumentException ex)
                {
                    throw new BusinessRuleException(ex.Message);
                }
            }
        }

        if (request.GlobalDiscountPct != 0)
        {
            try
            {
                quote.Update(
                    request.CustomerId, request.VehicleId, validUntil,
                    request.GlobalDiscountPct,
                    request.CustomerNotes, request.InternalNotes,
                    quote.Items);
            }
            catch (ArgumentException ex)
            {
                throw new BusinessRuleException(ex.Message);
            }
        }

        await _repo.AddAsync(quote, ct);
        await _repo.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await _repo.GetFullAsync(quote.Id, ct)
            ?? throw new InvalidOperationException("El presupuesto recién creado no se pudo leer.");
    }
}

/// <summary>
/// Lee la configuración del taller para obtener <see cref="Domain.Tenancy.TenantSettings.QuoteValidityDays"/>.
/// Implementado en Infrastructure.
/// </summary>
public interface ITenantSettingsReader
{
    Task<TenantSettingsSnapshot> GetAsync(Guid tenantId, CancellationToken ct);
}

/// <summary>Snapshot de las configuraciones que el handler de presupuestos necesita.</summary>
public sealed record TenantSettingsSnapshot(int QuoteValidityDays);
