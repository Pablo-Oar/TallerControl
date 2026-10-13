namespace TallerControl.Application.Users;

/// <summary>
/// Caso de uso: listar miembros del taller. Solo Owner puede invocar este endpoint (T-PERM-01).
/// </summary>
public sealed class ListMembersHandler
{
    private readonly IMembershipRepository _repo;

    public ListMembersHandler(IMembershipRepository repo)
    {
        _repo = repo;
    }

    public Task<IReadOnlyList<MemberDto>> HandleAsync(CancellationToken ct) =>
        _repo.ListAsync(ct);
}
