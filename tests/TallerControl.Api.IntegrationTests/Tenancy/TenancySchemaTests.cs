using Microsoft.EntityFrameworkCore;
using Npgsql;
using TallerControl.Api.IntegrationTests.Persistence;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Api.IntegrationTests.Tenancy;

/// <summary>E1: tenants, branches, tenant_settings y document_sequences (doc 04 §3.1, V2 cambio 1).</summary>
[Collection(PostgresCollection.Name)]
public sealed class TenancySchemaTests
{
    private readonly PostgresFixture _postgres;

    public TenancySchemaTests(PostgresFixture postgres)
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

    [Fact]
    public async Task Un_taller_nuevo_queda_activo_con_su_configuracion_por_defecto()
    {
        var db = await TenancyDb.CreateAsync(_postgres);
        await using var context = TenancyDb.CreateContext(db.ConnectionString, db.TenantId);

        var tenant = await context.Set<Tenant>().SingleAsync();
        var settings = await context.Set<TenantSettings>().SingleAsync();
        var branch = await context.Set<Branch>().SingleAsync();

        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Equal("America/Argentina/Buenos_Aires", tenant.TimeZone);
        Assert.True(branch.IsMain);
        Assert.Equal("Principal", branch.Name);
        Assert.Equal(21.00m, settings.DefaultVat);
        Assert.Equal(15, settings.QuoteValidityDays);
        Assert.Equal("54", settings.PhoneCountryCode);
        Assert.False(settings.QrPlateLookupEnabled);
    }

    [Fact]
    public async Task No_puede_haber_dos_sucursales_principales_en_un_taller()
    {
        var db = await TenancyDb.CreateAsync(_postgres);
        await using var context = TenancyDb.CreateContext(db.ConnectionString, db.TenantId);
        context.Add(new Branch(db.TenantId, "Otra", isMain: true));

        await AssertSqlStateAsync(PostgresErrorCodes.UniqueViolation, () => context.SaveChangesAsync());
    }

    [Fact]
    public async Task El_nombre_de_sucursal_es_unico_por_taller_pero_se_repite_entre_talleres()
    {
        var db = await TenancyDb.CreateAsync(_postgres);

        await using (var duplicate = TenancyDb.CreateContext(db.ConnectionString, db.TenantId))
        {
            duplicate.Add(new Branch(db.TenantId, Branch.MainName));
            await AssertSqlStateAsync(PostgresErrorCodes.UniqueViolation, () => duplicate.SaveChangesAsync());
        }

        // Otro taller puede tener su propia "Principal".
        var (otherTenant, _) = await TenancyDb.SeedTenantAsync(db.ConnectionString, "Otro taller");
        Assert.NotEqual(db.TenantId, otherTenant);
    }

    [Theory]
    [InlineData("UPDATE tenant_settings SET default_vat_rate = 12")]
    [InlineData("UPDATE tenant_settings SET quote_validity_days = 0")]
    [InlineData("UPDATE tenant_settings SET quote_validity_days = 181")]
    [InlineData("UPDATE tenants SET status = 'borrado'")]
    [InlineData("UPDATE tenants SET name = ''")]
    public async Task Los_CHECK_rechazan_valores_fuera_de_rango(string sql)
    {
        var db = await TenancyDb.CreateAsync(_postgres);

        await AssertSqlStateAsync(
            PostgresErrorCodes.CheckViolation,
            () => TenancyDb.ExecuteAsync(db.ConnectionString, sql));
    }

    [Fact]
    public async Task Una_secuencia_no_puede_apuntar_a_la_sucursal_de_otro_taller()
    {
        var db = await TenancyDb.CreateAsync(_postgres);
        var (otherTenant, _) = await TenancyDb.SeedTenantAsync(db.ConnectionString, "Otro taller");

        // Taller "otro" con la sucursal de db.TenantId: la FK compuesta lo impide.
        var sql = "INSERT INTO document_sequences (tenant_id, branch_id, kind, next_value) " +
                  $"VALUES ('{otherTenant}', '{db.BranchId}', 'quote', 1)";

        await AssertSqlStateAsync(
            PostgresErrorCodes.ForeignKeyViolation,
            () => TenancyDb.ExecuteAsync(db.ConnectionString, sql));
    }

    [Fact]
    public async Task El_borrado_fisico_de_un_taller_con_datos_esta_prohibido()
    {
        var db = await TenancyDb.CreateAsync(_postgres);

        // FK con ON DELETE RESTRICT: PostgreSQL responde restrict_violation (23001).
        await AssertSqlStateAsync(
            PostgresErrorCodes.RestrictViolation,
            () => TenancyDb.ExecuteAsync(db.ConnectionString, $"DELETE FROM tenants WHERE id = '{db.TenantId}'"));
    }
}
