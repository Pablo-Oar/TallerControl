using System.Text.RegularExpressions;

namespace TallerControl.Domain.Common;

/// <summary>Formato mínimo de un email (algo@dominio.tld, sin espacios). No prueba que exista.</summary>
public static partial class EmailFormat
{
    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 254 && Pattern().IsMatch(value.Trim());

    /// <summary>Normaliza (recorta, minúsculas); vacío → nulo. Lanza si no tiene formato de email.</summary>
    public static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!IsValid(value))
            throw new ArgumentException("El email no es válido: revisá que tenga la forma nombre@dominio.com.", nameof(value));
        return value.Trim().ToLowerInvariant();
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$")]
    private static partial Regex Pattern();
}
