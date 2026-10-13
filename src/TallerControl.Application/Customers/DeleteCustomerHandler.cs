using TallerControl.Application.Common;

namespace TallerControl.Application.Customers;

/// <summary>
/// Caso de uso: borrado lógico del cliente (doc 05 §2.3, T-CUS-07).
/// No se puede borrar si tiene saldo pendiente (verificado en E4 con órdenes).
/// </summary>
public sealed class DeleteCustomerHandler
{
    private readonly ICustomerRepository _repo;
    private readonly IActorContext _actorContext;
    private readonly TimeProvider _clock;

    public DeleteCustomerHandler(ICustomerRepository repo, IActorContext actorContext, TimeProvider clock)
    {
        _repo = repo;
        _actorContext = actorContext;
        _clock = clock;
    }

    public async Task HandleAsync(Guid id, string reason, CancellationToken ct)
    {
        var customer = await _repo.FindActiveByIdAsync(id, ct)
            ?? throw new NotFoundException();

        var balance = await _repo.GetPendingBalanceAsync(id, ct);
        if (balance > 0)
            throw new BusinessRuleException("No se puede borrar un cliente con saldo pendiente.");

        customer.Delete(reason, _actorContext.ActorId, _clock.GetUtcNow());
        await _repo.SaveChangesAsync(ct);
    }
}
