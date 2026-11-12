using System.Text;

namespace TallerControl.Application.Common;

/// <summary>Texto de un enum como lo espera el frontend: <c>InProgress</c> → <c>in_progress</c>.</summary>
public static class EnumNames
{
    public static string ToSnake<TEnum>(this TEnum value)
        where TEnum : struct, Enum
    {
        var name = value.ToString();
        var builder = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0) builder.Append('_');
                builder.Append(char.ToLowerInvariant(c));
            }
            else builder.Append(c);
        }

        return builder.ToString();
    }
}
