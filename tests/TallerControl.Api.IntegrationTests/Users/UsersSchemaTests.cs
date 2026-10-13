using Microsoft.EntityFrameworkCore;
using Npgsql;
using TallerControl.Api.IntegrationTests.Persistence;
using TallerControl.Api.IntegrationTests.Tenancy;
using TallerControl.Domain.Users;

namespace TallerControl.Api.IntegrationTests.Users;

/// <summary>
/// E2 bloque 1: tablas <c>users</c>, <c>memberships</c>, <c>invitations</c> (doc 04 §3.1).
/// Verifica unicidad, CHECKs y FKs con RESTRICT (REQ-02).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class UsersSchemaTests
{
    private readonly PostgresFixture _postgres;

    public UsersSchemaTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    private static async Task AssertSqlStateAsync(string expected, Func<Task> action)
    {
        var error = await Assert.ThrowsAnyAsync<Exception>(action);
        var postgres = error as PostgresException ?? error.InnerException as PostgresException;
        Assert.NotNull(postgres);
        Assert.Equal(expected, postgres.SqlState);
    }

    private async Task<(string ConnectionString, Guid TenantId)> SeedTenantAsync()
    {
        var cs = await _postgres.CreateDatabaseAsync();
        var context = TenancyDb.CreateContext(cs, tenantId: null);
        await context.Database.MigrateAsync();
        var (tenantId, _) = await TenancyDb.SeedTenantAsync(cs, "Taller");
        await context.DisposeAsync();
        return (cs, tenantId);
    }

    [Fact]
    public async Task Dos_usuarios_no_pueden_compartir_email_sin_importar_mayusculas()
    {
        var (cs, _) = await SeedTenantAsync();
        await using var context = TenancyDb.CreateContext(cs, tenantId: null);

        context.Add(new User("pablo@taller.com", "Pablo"));
        await context.SaveChangesAsync();

        context.Add(new User("PABLO@Taller.COM", "Otro"));
        await AssertSqlStateAsync(PostgresErrorCodes.UniqueViolation, () => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Un_estado_fuera_del_CHECK_es_rechazado()
    {
        var (cs, _) = await SeedTenantAsync();

        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation, () => TenancyDb.ExecuteAsync(cs,
            "INSERT INTO users (id, email, display_name, status, security_stamp) " +
            "VALUES (gen_random_uuid(), 'x@taller.com', 'X', 'borrado', 'stamp')"));
    }

    [Fact]
    public async Task No_puede_haber_dos_membresias_del_mismo_usuario_en_el_mismo_taller()
    {
        var (cs, tenantId) = await SeedTenantAsync();
        await using var context = TenancyDb.CreateContext(cs, tenantId);

        var user = new User("pablo@taller.com", "Pablo");
        context.Add(user);
        await context.SaveChangesAsync();

        context.Add(new Membership(tenantId, user.Id, TenantRole.Owner));
        await context.SaveChangesAsync();

        context.Add(new Membership(tenantId, user.Id, TenantRole.AdminStaff));
        await AssertSqlStateAsync(PostgresErrorCodes.UniqueViolation, () => context.SaveChangesAsync());
    }

    [Fact]
    public async Task El_rol_debe_estar_en_la_lista_permitida()
    {
        var (cs, tenantId) = await SeedTenantAsync();
        await using var context = TenancyDb.CreateContext(cs, tenantId: null);
        var user = new User("x@taller.com", "X");
        context.Add(user);
        await context.SaveChangesAsync();

        await AssertSqlStateAsync(PostgresErrorCodes.CheckViolation, () => TenancyDb.ExecuteAsync(cs,
            $"INSERT INTO memberships (id, tenant_id, user_id, role, permissions, status) " +
            $"VALUES (gen_random_uuid(), '{tenantId}', '{user.Id}', 'super_admin', ARRAY[]::text[], 'active')"));
    }

    [Fact]
    public async Task No_se_puede_borrar_un_usuario_con_membresia()
    {
        var (cs, tenantId) = await SeedTenantAsync();
        await using var context = TenancyDb.CreateContext(cs, tenantId);
        var user = new User("x@taller.com", "X");
        context.Add(user);
        context.Add(new Membership(tenantId, user.Id, TenantRole.Owner));
        await context.SaveChangesAsync();

        await AssertSqlStateAsync(PostgresErrorCodes.RestrictViolation,
            () => TenancyDb.ExecuteAsync(cs, $"DELETE FROM users WHERE id = '{user.Id}'"));
    }

    [Fact]
    public async Task Dos_invitaciones_no_pueden_compartir_el_hash_del_token()
    {
        var (cs, tenantId) = await SeedTenantAsync();
        await using var context = TenancyDb.CreateContext(cs, tenantId);

        var (i1, _) = Invitation.Create(tenantId, "a@taller.com", TenantRole.Mechanic, null, DateTimeOffset.UtcNow);
        context.Add(i1);
        await context.SaveChangesAsync();

        await AssertSqlStateAsync(PostgresErrorCodes.UniqueViolation, () => TenancyDb.ExecuteAsync(cs,
            "INSERT INTO invitations (id, tenant_id, email, role, permissions, token_hash, expires_at) " +
            $"VALUES (gen_random_uuid(), '{tenantId}', 'b@taller.com', 'mechanic', ARRAY[]::text[], " +
            $"decode('{Convert.ToHexString(i1.TokenHash)}', 'hex'), now() + interval '24 hours')"));
    }
}
