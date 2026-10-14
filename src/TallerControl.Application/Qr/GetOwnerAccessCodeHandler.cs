using TallerControl.Application.Common;
using TallerControl.Application.Customers;

namespace TallerControl.Application.Qr;

public sealed class GetOwnerAccessCodeHandler(ICustomerRepository customers)
{
    /// <summary>
    /// Devuelve el código de acceso del dueño en texto claro.
    /// Si no existe, lo genera y persiste el hash (se muestra solo una vez).
    /// </summary>
    public async Task<OwnerAccessCodeDto> HandleAsync(Guid customerId, CancellationToken ct)
    {
        var customer = await customers.FindActiveByIdAsync(customerId, ct)
            ?? throw new NotFoundException("Cliente no encontrado.");

        var code = customer.GenerateOwnerAccessCode();
        await customers.SaveChangesAsync(ct);

        return new OwnerAccessCodeDto(code);
    }
}
