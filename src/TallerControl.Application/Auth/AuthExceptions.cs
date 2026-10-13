namespace TallerControl.Application.Auth;

/// <summary>
/// Credenciales inválidas. Mensaje genérico idéntico para email inexistente, contraseña incorrecta
/// y usuario deshabilitado: el servidor nunca revela cuál de los tres falló (doc 07 §2, T-AUTH-02).
/// </summary>
public sealed class AuthenticationException : Exception
{
    public AuthenticationException()
        : base("Email o contraseña incorrectos.")
    {
    }
}

/// <summary>
/// La cuenta está bloqueada temporalmente tras <see cref="Domain.Users.User.MaxFailedAttempts"/>
/// intentos fallidos (REQ-02, T-AUTH-03). La API responde 423.
/// </summary>
public sealed class AccountLockedException : Exception
{
    public AccountLockedException(DateTimeOffset lockoutEnd)
        : base("La cuenta está bloqueada temporalmente.")
    {
        LockoutEnd = lockoutEnd;
    }

    public DateTimeOffset LockoutEnd { get; }
}
