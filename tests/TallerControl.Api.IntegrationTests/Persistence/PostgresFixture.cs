using Npgsql;
using Testcontainers.PostgreSql;

namespace TallerControl.Api.IntegrationTests.Persistence;

/// <summary>Un contenedor PostgreSQL 18 por ejecución; cada prueba crea su propia base.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // Cada base de prueba abre su propio pool de Npgsql: sin esto, con unas decenas de pruebas se
    // agotan las 100 conexiones por defecto ("53300 too many clients already").
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18")
        .WithCommand("-c", "max_connections=400")
        .Build();

    public const string AppRoleName = "tallercontrol_app";

    private const string AppRolePassword = "app-role-test-password";

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // El rol de la aplicación existe antes de migrar, como en dev.ps1 y en producción:
        // la migración AppRolePermissions le otorga los permisos solo si ya existe.
        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            $"CREATE ROLE {AppRoleName} LOGIN PASSWORD '{AppRolePassword}'", connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>La misma base, conectada como el rol de la aplicación (sin permisos de dueño).</summary>
    public static string AsAppRole(string ownerConnectionString) =>
        new NpgsqlConnectionStringBuilder(ownerConnectionString)
        {
            Username = AppRoleName,
            MaxPoolSize = 20,
            ConnectionIdleLifetime = 5,
            ConnectionPruningInterval = 1,
            Password = AppRolePassword,
        }.ConnectionString;

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Crea una base vacía y devuelve su cadena de conexión.</summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = $"t_{Guid.NewGuid():N}";

        await using (var connection = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = name,
            ConnectionIdleLifetime = 5, // libera conexiones ociosas: cada base de prueba tiene su pool
            ConnectionPruningInterval = 1,
            MaxPoolSize = 60,
        }.ConnectionString;
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
