using TallerControl.Application.Common;
using TallerControl.Application.Work;

namespace TallerControl.Application.Scheduling;

/// <summary>
/// Convierte un turno en orden de trabajo (T-APT-02).
/// Exige el km actual del vehículo; 422 si se omite.
/// </summary>
public sealed class ConvertAppointmentToWorkOrderHandler(
    ISchedulingRepository schedulingRepo,
    CreateWorkOrderHandler createWorkOrder)
{
    public async Task<WorkOrderDto> HandleAsync(
        Guid appointmentId,
        ConvertToWorkOrderRequest request,
        CancellationToken ct)
    {
        var appointment = await schedulingRepo.FindByIdAsync(appointmentId, ct)
            ?? throw new NotFoundException();

        if (request.OdometerKm <= 0)
            throw new BusinessRuleException("El kilometraje es obligatorio para crear la orden desde un turno.");

        // Crear la orden de trabajo usando los datos del turno.
        var items = appointment.Services.Select(s => new WorkOrderItemRequest(
            Kind: Domain.Work.WorkOrderItemKind.Service,
            Description: string.Empty,
            CatalogItemId: s.CatalogItemId,
            Quantity: 1,
            UnitNetPrice: 0,
            VatRate: 0)).ToList();

        var woRequest = new CreateWorkOrderRequest(
            CustomerId: appointment.CustomerId,
            VehicleId: appointment.VehicleId,
            OdometerKm: request.OdometerKm,
            FuelLevel: null,
            ReportedIssue: null,
            Priority: Domain.Work.WorkOrderPriority.Normal,
            AssignedMechanicId: appointment.MechanicMembershipId,
            Items: items);

        var workOrder = await createWorkOrder.HandleAsync(woRequest, ct);

        try
        {
            appointment.Complete();
        }
        catch (InvalidOperationException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }

        await schedulingRepo.SaveChangesAsync(ct);

        return workOrder;
    }
}
