using System.Text.RegularExpressions;

namespace TallerControl.Domain.Customers;

/// <summary>
/// Normaliza números de teléfono argentinos a E.164 (doc 04 §3.2, T-CUS-04).
/// Soporta formatos locales: 011 15-XXXX-XXXX, 0XXX 15-XXXX-XXXX y +54… internacionales.
/// </summary>
public static partial class PhoneNormalizer
{
    [GeneratedRegex(@"[^\d+]")]
    private static partial Regex NonDigitOrPlus();

    [GeneratedRegex(@"^\+\d{7,15}$")]
    private static partial Regex E164Format();

    /// <summary>
    /// Normaliza el teléfono a E.164 o devuelve <c>null</c> si el parámetro es nulo.
    /// Lanza <see cref="ArgumentException"/> si el formato no se puede normalizar.
    /// </summary>
    public static string? Normalize(string? phone)
    {
        if (phone is null) return null;

        var stripped = NonDigitOrPlus().Replace(phone, "");

        if (stripped.Length == 0)
            throw new ArgumentException("El teléfono no puede estar vacío.", nameof(phone));

        // 54… / 549… escrito sin el '+'
        if (!stripped.StartsWith('+') && !stripped.StartsWith('0') && stripped.StartsWith("54") && stripped.Length >= 12)
            stripped = "+" + stripped;

        // 10 dígitos sin el 0 de discado (3416565879, 1145678901): se toman como celular con área
        if (!stripped.StartsWith('+') && !stripped.StartsWith('0') && stripped.Length == 10)
            return $"+549{stripped}";

        // Ya en formato internacional
        if (stripped.StartsWith('+'))
        {
            if (!E164Format().IsMatch(stripped))
                throw new ArgumentException("Formato de teléfono inválido.", nameof(phone));
            return stripped;
        }

        // Formato local argentino: empieza con 0
        if (stripped.StartsWith('0'))
        {
            var rest = stripped[1..]; // quita el 0 de discado

            // Área de 2 dígitos (Buenos Aires: 11)
            if (rest.Length >= 2 && rest[..2] == "11")
            {
                var afterArea = rest[2..];
                // Móvil: 15 + 7 dígitos → +549 11 XXXXXXX
                if (afterArea.StartsWith("15") && afterArea.Length == 9)
                    return $"+549{rest[..2]}{afterArea[2..]}";
                // Fijo: 8 dígitos → +54 11 XXXXXXXX
                if (afterArea.Length == 8)
                    return $"+54{rest[..2]}{afterArea}";
            }

            // Área de 3 dígitos (ej: 351 Córdoba, 341 Rosario)
            if (rest.Length >= 3)
            {
                var area3 = rest[..3];
                var afterArea3 = rest[3..];
                if (afterArea3.StartsWith("15") && afterArea3.Length == 9)
                    return $"+549{area3}{afterArea3[2..]}";
                if (afterArea3.Length == 8)
                    return $"+54{area3}{afterArea3}";
            }

            // Área de 4 dígitos (ej: 2901 Ushuaia)
            if (rest.Length >= 4)
            {
                var area4 = rest[..4];
                var afterArea4 = rest[4..];
                if (afterArea4.StartsWith("15") && afterArea4.Length == 9)
                    return $"+549{area4}{afterArea4[2..]}";
                if (afterArea4.Length == 7)
                    return $"+54{area4}{afterArea4}";
            }
        }

        throw new ArgumentException(
            "No se pudo normalizar el teléfono. Probá con el código de área y el número (ej. 341 6565879), con el 0 (0341 15-6565879) o con +54.");
    }
}
