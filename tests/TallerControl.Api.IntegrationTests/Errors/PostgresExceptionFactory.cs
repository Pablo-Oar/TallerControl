using Npgsql;

namespace TallerControl.Api.IntegrationTests.Errors;

/// <summary>Fabrica un <see cref="PostgresException"/> con el SQLSTATE indicado sin ir a la base.</summary>
internal static class PostgresExceptionFactory
{
    public static PostgresException Create(string sqlState) =>
        new("duplicado", "ERROR", "ERROR", sqlState);
}
