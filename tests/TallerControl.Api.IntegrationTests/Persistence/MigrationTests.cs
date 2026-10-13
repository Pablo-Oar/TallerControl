using Microsoft.EntityFrameworkCore;
using Npgsql;
using TallerControl.Infrastructure.Persistence;


namespace TallerControl.Api.IntegrationTests.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class MigrationTests
{
    private readonly PostgresFixture _postgres;

    public MigrationTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task Las_migraciones_se_aplican_desde_cero_y_el_modelo_no_tiene_cambios_pendientes()
    {
        var connectionString = await _postgres.CreateDatabaseAsync();
        var factory = new AppDbContextFactoryProxy(connectionString);
        await using var context = factory.Create();

        await context.Database.MigrateAsync();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(
            context.Database.HasPendingModelChanges(),
            "El modelo cambiÃ³ y falta una migraciÃ³n: dotnet ef migrations add ...");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT extname FROM pg_extension WHERE extname IN ('citext','pg_trgm') ORDER BY extname", connection);
        var extensions = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            extensions.Add(reader.GetString(0));
        }

        Assert.Equal(["citext", "pg_trgm"], extensions);
    }

    /// <summary>Crea el contexto real con la misma configuraciÃ³n que la aplicaciÃ³n.</summary>
    private sealed class AppDbContextFactoryProxy
    {
        private readonly string _connectionString;

        public AppDbContextFactoryProxy(string connectionString)
        {
            _connectionString = connectionString;
        }

        public AppDbContext Create()
        {
            var builder = new DbContextOptionsBuilder<AppDbContext>();
            builder.UseTallerControlNpgsql(_connectionString);
            return new AppDbContext(builder.Options, new TestTenantContext(), new TestActorContext(), TimeProvider.System);
        }
    }
}
