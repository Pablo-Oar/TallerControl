namespace TallerControl.Application.Common;

/// <summary>
/// Encola un efecto externo (por ejemplo un email) en la transacción en curso. El mensaje queda
/// confirmado o revertido junto con el cambio de negocio que lo originó; no se envía nada aquí.
/// </summary>
public interface IOutbox
{
    void Enqueue(string type, object payload);
}
