using TallerControl.Application.Common;
using TallerControl.Domain.Scheduling;

namespace TallerControl.Application.Scheduling;

/// <summary>Handlers para gestionar reglas y excepciones de disponibilidad de mecánicos.</summary>
public sealed class ManageAvailabilityHandler(ISchedulingRepository repo, ITenantContext tenant)
{
    public async Task<IReadOnlyList<AvailabilityRuleDto>> GetRulesAsync(
        Guid? mechanicMembershipId, CancellationToken ct)
    {
        return await repo.GetRulesAsync(mechanicMembershipId, ct);
    }

    public async Task<AvailabilityRuleDto> CreateRuleAsync(
        UpsertAvailabilityRuleRequest request, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        if (!await repo.MembershipExistsAsync(request.MechanicMembershipId, ct))
            throw new NotFoundException("El mecánico no existe.");

        AvailabilityRule rule;
        try
        {
            rule = new AvailabilityRule(
                tenantId,
                request.MechanicMembershipId,
                request.DayOfWeek,
                request.StartTime,
                request.EndTime);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await repo.AddRuleAsync(rule, ct);
        await repo.SaveChangesAsync(ct);

        return (await repo.GetRulesAsync(request.MechanicMembershipId, ct))
            .First(r => r.Id == rule.Id);
    }

    public async Task<AvailabilityRuleDto> UpdateRuleAsync(
        Guid id, UpsertAvailabilityRuleRequest request, CancellationToken ct)
    {
        var rule = await repo.FindRuleByIdAsync(id, ct)
            ?? throw new NotFoundException();

        try
        {
            rule.Update(request.DayOfWeek, request.StartTime, request.EndTime);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await repo.SaveChangesAsync(ct);

        return (await repo.GetRulesAsync(rule.MechanicMembershipId, ct))
            .First(r => r.Id == rule.Id);
    }

    public async Task DeleteRuleAsync(Guid id, CancellationToken ct)
    {
        _ = await repo.FindRuleByIdAsync(id, ct)
            ?? throw new NotFoundException();

        // Las reglas se eliminan físicamente (no son datos de negocio históricos).
        await repo.DeleteRuleAsync(id, ct);
        await repo.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<AvailabilityExceptionDto>> GetExceptionsRangeAsync(
        DateOnly from, DateOnly to, CancellationToken ct) =>
        repo.GetExceptionsRangeAsync(from, to, ct);

    public async Task<IReadOnlyList<AvailabilityExceptionDto>> GetExceptionsAsync(
        DateOnly date, CancellationToken ct)
    {
        return await repo.GetExceptionsAsync(date, ct);
    }

    public async Task<AvailabilityExceptionDto> CreateExceptionAsync(
        CreateAvailabilityExceptionRequest request, CancellationToken ct)
    {
        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        if (request.MechanicMembershipId.HasValue
            && !await repo.MembershipExistsAsync(request.MechanicMembershipId.Value, ct))
            throw new NotFoundException("El mecánico no existe.");

        AvailabilityException ex2;
        try
        {
            ex2 = new AvailabilityException(
                tenantId,
                request.MechanicMembershipId,
                request.Date,
                request.IsBlocked,
                request.StartTime,
                request.EndTime,
                request.Reason);
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await repo.AddExceptionAsync(ex2, ct);
        await repo.SaveChangesAsync(ct);

        return (await repo.GetExceptionsAsync(request.Date, ct))
            .First(e => e.Id == ex2.Id);
    }

    public async Task DeleteExceptionAsync(Guid id, CancellationToken ct)
    {
        var exception = await repo.FindExceptionByIdAsync(id, ct)
            ?? throw new NotFoundException();

        repo.RemoveException(exception);
        await repo.SaveChangesAsync(ct);
    }
}
