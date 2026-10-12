using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace TallerControl.Infrastructure.Health;

/// <summary>Comprueba que PostgreSQL acepta conexiones y responde una consulta trivial.</summary>
internal sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly NpgsqlDataSource _dataSource;

    public DatabaseHealthCheck(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = _dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("La base de datos no responde.", ex);
        }
    }
}
