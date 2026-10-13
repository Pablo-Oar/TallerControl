namespace TallerControl.Application.Common;

/// <summary>
/// El recurso no existe o pertenece a otro taller: ambos casos responden 404, para no confirmar
/// la existencia de datos ajenos (REQ-01).
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message = "El recurso no existe.")
        : base(message)
    {
    }
}

/// <summary>Regla de negocio incumplida: responde 422, opcionalmente con errores por campo.</summary>
public sealed class BusinessRuleException : Exception
{
    public BusinessRuleException(string message, IReadOnlyDictionary<string, string[]>? errors = null)
        : base(message)
    {
        Errors = errors ?? new Dictionary<string, string[]>();
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

/// <summary>Conflicto de estado o duplicado: responde 409.</summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Versión del recurso (ETag / If-Match) no coincide: responde 412 (doc 05 §1, REQ-03).
/// </summary>
public sealed class ConcurrencyException : Exception
{
    public ConcurrencyException()
        : base("El recurso fue modificado por otra operación. Recargá y volvé a intentar.")
    {
    }
}
