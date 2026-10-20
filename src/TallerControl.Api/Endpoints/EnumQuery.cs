namespace TallerControl.Api.Endpoints;

/// <summary>
/// Parseo de enums recibidos por query string. La API serializa en snake_case (in_progress),
/// así que se ignoran los guiones bajos y las mayúsculas: in_progress, InProgress e IN_PROGRESS valen igual.
/// </summary>
public static class EnumQuery
{
    public static bool TryParse<T>(string? value, out T result) where T : struct, Enum
    {
        result = default;
        return value is not null
            && Enum.TryParse(value.Replace("_", "", StringComparison.Ordinal), ignoreCase: true, out result)
            && Enum.IsDefined(result);
    }
}
