namespace TallerControl.Application.Common;

/// <summary>Fecha de hoy en Argentina (sin horario de verano): para vigencias que son días calendario.</summary>
public static class ArgentinaTime
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(-3);

    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(now.ToOffset(Offset).DateTime);
}
