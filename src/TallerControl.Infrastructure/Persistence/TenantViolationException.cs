namespace TallerControl.Infrastructure.Persistence;

/// <summary>Se intentó escribir una entidad de otro taller, o sin taller en la sesión.</summary>
public sealed class TenantViolationException : InvalidOperationException
{
    public TenantViolationException(string message)
        : base(message)
    {
    }
}
