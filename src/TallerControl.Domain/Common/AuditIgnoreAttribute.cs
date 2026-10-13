namespace TallerControl.Domain.Common;

/// <summary>
/// La propiedad no se copia a la auditoría: contraseñas, tokens, OTP, documentos completos
/// (doc 07 §9). El resto de las propiedades de una entidad <see cref="IAudited"/> sí se registran.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AuditIgnoreAttribute : Attribute
{
}
