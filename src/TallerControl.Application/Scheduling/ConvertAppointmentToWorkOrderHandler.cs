using TallerControl.Application.Catalog;
using TallerControl.Application.Common;
using TallerControl.Application.Work;

namespace TallerControl.Application.Scheduling;

/// <summary>
/// Convierte un turno en orden de trabajo (T-APT-02).
/// Exige el km actual del vehículo; 422 si se omite.
/// </summary>
public sealed class ConvertAppointmentToWorkOrderHandler(
    ISchedulingRepository schedulingRepo,
    ICatalogRepository catalogRepo,
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

        // Cada servicio del turno se copia a la orden con el nombre, el precio y el IVA vigentes del catálogo.
        var items = new List<WorkOrderItemRequest>();
        foreach (var service in appointment.Services)
        {
            var catalogItem = await catalogRepo.FindActiveByIdAsync(service.CatalogItemId, ct)
                ?? throw new BusinessRuleException("Un servicio del turno ya no existe en el catálogo.");

            items.Add(new WorkOrderItemRequest(
                Kind: Domain.Work.WorkOrderItemKind.Service,
                Description: catalogItem.Name,
                CatalogItemId: catalogItem.Id,
                Quantity: 1,
                UnitNetPrice: catalogItem.NetPrice,
                VatRate: catalogItem.VatRate));
        }

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
