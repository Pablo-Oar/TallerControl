using Microsoft.EntityFrameworkCore;
using Npgsql;
using TallerControl.Api.IntegrationTests.Persistence;
using TallerControl.Api.IntegrationTests.Tenancy;
using TallerControl.Domain.Audit;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Api.IntegrationTests.Security;

/// <summary>
/// El rol con el que corre la aplicación no puede borrar filas ni alterar la auditoría
/// (REQ-19 CA 2, doc 04 §1 "borrado físico prohibido", doc 07 §9).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AppRolePermissionTests
{
    private readonly PostgresFixture _postgres;

    public AppRolePermissionTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    private static async Task AssertDeniedAsync(string connectionString, string sql)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(
            () => TenancyDb.ExecuteAsync(connectionString, sql));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    [Fact]
    public async Task La_aplicacion_lee_inserta_y_actualiza_con_su_rol_y_deja_auditoria()
    {
        var env = await TenancyDb.CreateAsync(_postgres);
        var appConnection = PostgresFixture.AsAppRole(env.ConnectionString);

        await using (var app = TenancyDb.CreateContext(appConnection, env.TenantId))
        {
            var branch = new Branch(env.TenantId, "Centro");
            app.Add(branch);
            await app.SaveChangesAsync();

            var settings = await app.Set<TenantSettings>().SingleAsync();
            Assert.Equal(21.00m, settings.DefaultVat);
        }

        await using var read = TenancyDb.CreateContext(appConnection, env.TenantId);
        Assert.Equal(2, await read.Set<Branch>().CountAsync());
        Assert.Contains(
            await read.Set<AuditEvent>().Select(e => e.Action).ToListAsync(),
            a => a == "branch.created");
    }

    [Theory]
    [InlineData("DELETE FROM branches")]
    [InlineData("DELETE FROM tenants")]
    [InlineData("DELETE FROM outbox_messages")]
    [InlineData("TRUNCATE branches CASCADE")]
    public async Task La_aplicacion_no_puede_borrar_filas(string sql)
    {
        var env = await TenancyDb.CreateAsync(_postgres);

        await AssertDeniedAsync(PostgresFixture.AsAppRole(env.ConnectionString), sql);
    }

    [Fact]
    public async Task La_aplicacion_inserta_pero_no_modifica_la_auditoria()
    {
        var env = await TenancyDb.CreateAsync(_postgres);
        var appConnection = PostgresFixture.AsAppRole(env.ConnectionString);

        var insert = "INSERT INTO audit_events (id, tenant_id, actor_type, entity_type, entity_id, action, changes, occurred_at) " +
                     $"VALUES (gen_random_uuid(), '{env.TenantId}', 'system', 'x', gen_random_uuid(), 'x.created', '{{}}', now())";
        Assert.Equal(1, await TenancyDb.ExecuteAsync(appConnection, insert));

        await AssertDeniedAsync(appConnection, "UPDATE audit_events SET action = 'x'");
        await AssertDeniedAsync(appConnection, "DELETE FROM audit_events");
    }

    [Theory]
    [InlineData("CREATE TABLE intruso (id int)")]
    [InlineData("DROP TABLE branches")]
    [InlineData("ALTER TABLE branches ADD COLUMN x int")]
    [InlineData("SELECT * FROM \"__EFMigrationsHistory\"")]
    public async Task La_aplicacion_no_puede_cambiar_el_esquema_ni_ver_el_historial_de_migraciones(string sql)
    {
        var env = await TenancyDb.CreateAsync(_postgres);

        await AssertDeniedAsync(PostgresFixture.AsAppRole(env.ConnectionString), sql);
    }

    [Fact]
    public async Task Una_tabla_nueva_del_dueno_hereda_los_permisos_por_defecto()
    {
        var env = await TenancyDb.CreateAsync(_postgres);
        var appConnection = PostgresFixture.AsAppRole(env.ConnectionString);

        await TenancyDb.ExecuteAsync(env.ConnectionString, "CREATE TABLE futura (id int PRIMARY KEY, valor text)");

        Assert.Equal(1, await TenancyDb.ExecuteAsync(appConnection, "INSERT INTO futura VALUES (1, 'a')"));
        Assert.Equal(1, await TenancyDb.ExecuteAsync(appConnection, "UPDATE futura SET valor = 'b'"));
        await AssertDeniedAsync(appConnection, "DELETE FROM futura");
    }

    [Fact]
    public async Task El_rol_de_la_aplicacion_no_es_superusuario_ni_puede_crear_roles()
    {
        var env = await TenancyDb.CreateAsync(_postgres);
        var appConnection = PostgresFixture.AsAppRole(env.ConnectionString);

        await using var connection = new NpgsqlConnection(appConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT rolsuper OR rolcreaterole OR rolcreatedb FROM pg_roles WHERE rolname = current_user",
            connection);

        Assert.False((bool)(await command.ExecuteScalarAsync())!);
    }
}
