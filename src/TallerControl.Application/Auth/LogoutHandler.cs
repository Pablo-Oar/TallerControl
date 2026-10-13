namespace TallerControl.Application.Auth;

/// <summary>
/// Rota el <c>security_stamp</c> del usuario para invalidar cualquier cookie residual aunque alguien
/// la hubiera copiado (doc 05 §2, T-AUTH-05). El endpoint, además, limpia la cookie local.
/// </summary>
public sealed class LogoutHandler
{
    private readonly IAuthRepository _repository;

    public LogoutHandler(IAuthRepository repository)
    {
        _repository = repository;
    }

    public async Task HandleAsync(Guid userId, CancellationToken ct)
    {
        var user = await _repository.FindUserByIdAsync(userId, ct);
        if (user is null)
        {
            return;
        }

        user.RotateSecurityStamp();
        await _repository.SaveChangesAsync(ct);
    }
}
