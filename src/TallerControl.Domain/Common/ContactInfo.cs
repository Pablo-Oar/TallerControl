namespace TallerControl.Domain.Common;

/// <summary>Regla de contacto de clientes y proveedores: al menos un teléfono o un email.</summary>
public static class ContactInfo
{
    public const string RequiredMessage = "Cargá al menos un dato de contacto: teléfono o email.";

    public static void EnsureAny(string? email, string? phone)
    {
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException(RequiredMessage);
    }
}
