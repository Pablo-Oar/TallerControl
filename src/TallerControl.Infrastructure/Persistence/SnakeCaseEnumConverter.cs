using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TallerControl.Infrastructure.Persistence;

/// <summary>Convierte nombres de enum a su texto en la base: <c>InProgress</c> se guarda como <c>in_progress</c>.</summary>
internal static class EnumText
{
    public static string ToSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}

internal static class EnumTextMap<TEnum>
    where TEnum : struct, Enum
{
    private static readonly Dictionary<TEnum, string> ToText =
        Enum.GetValues<TEnum>().ToDictionary(v => v, v => EnumText.ToSnakeCase(v.ToString()));

    private static readonly Dictionary<string, TEnum> FromText =
        ToText.ToDictionary(p => p.Value, p => p.Key);

    public static IReadOnlyCollection<string> Values => FromText.Keys;

    public static string Write(TEnum value) => ToText[value];

    public static TEnum Read(string text) => FromText[text];
}

internal sealed class SnakeCaseEnumConverter<TEnum> : ValueConverter<TEnum, string>
    where TEnum : struct, Enum
{
    public SnakeCaseEnumConverter()
        : base(v => EnumTextMap<TEnum>.Write(v), s => EnumTextMap<TEnum>.Read(s))
    {
    }
}
