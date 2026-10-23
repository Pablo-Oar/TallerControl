namespace TallerControl.Domain.Common;

/// <summary>CUIT/CUIL argentino: 11 dígitos con dígito verificador (módulo 11).</summary>
public static class Cuit
{
    private static readonly int[] Weights = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

    /// <summary>¿Tiene 11 dígitos y el verificador es el correcto? Acepta guiones y espacios.</summary>
    public static bool IsValid(string? value)
    {
        var digits = Digits(value);
        if (digits is null) return false;

        var sum = 0;
        for (var i = 0; i < 10; i++) sum += (digits[i] - '0') * Weights[i];

        var check = 11 - (sum % 11);
        if (check == 11) check = 0;
        if (check == 10) return false; // no existe CUIT con ese cuerpo

        return check == digits[10] - '0';
    }

    /// <summary>"20123456786" → "20-12345678-6". Nulo si no tiene 11 dígitos.</summary>
    public static string? Format(string? value)
    {
        var digits = Digits(value);
        return digits is null ? null : $"{digits[..2]}-{digits[2..10]}-{digits[10]}";
    }

    private static string? Digits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length == 11 && value.All(c => char.IsDigit(c) || c is '-' or ' ') ? digits : null;
    }
}
