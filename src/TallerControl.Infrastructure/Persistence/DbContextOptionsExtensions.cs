using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TallerControl.Infrastructure.Persistence;

public static class DbContextOptionsExtensions
{
    /// <summary>Npgsql más las convenciones de nombres del proyecto (snake_case).</summary>
    public static DbContextOptionsBuilder UseTallerControlNpgsql(
        this DbContextOptionsBuilder builder,
        NpgsqlDataSource dataSource) =>
        builder.UseNpgsql(dataSource).UseSnakeCaseNamingConvention();

    public static DbContextOptionsBuilder UseTallerControlNpgsql(
        this DbContextOptionsBuilder builder,
        string connectionString) =>
        builder.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
}
