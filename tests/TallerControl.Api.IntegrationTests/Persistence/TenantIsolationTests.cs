using Microsoft.EntityFrameworkCore;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Api.IntegrationTests.Persistence;

/// <summary>REQ-01: aislamiento entre talleres a nivel de persistencia (CA 2 y 3).</summary>
[Collection(PostgresCollection.Name)]
public sealed class TenantIsolationTests
{
    private static readonly Guid TenantA = Guid.CreateVersion7();
    private static readonly Guid TenantB = Guid.CreateVersion7();

    private readonly PostgresFixture _postgres;

    public TenantIsolationTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    private async Task<string> NewDatabaseAsync()
    {
        var connectionString = await _postgres.CreateDatabaseAsync();
        await using var context = TestDbContext.Create(connectionString, TenantA);
        await context.Database.EnsureCreatedAsync();
        return connectionString;
    }

    private static TestItem NewItem(string name) =>
        new(name, TestStatus.Active, 1m, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Las_altas_toman_el_taller_de_la_sesion()
    {
        var connectionString = await NewDatabaseAsync();
        await using var context = TestDbContext.Create(connectionString, TenantA);
        var item = NewItem("a");

        context.Items.Add(item);
        await context.SaveChangesAsync();

        Assert.Equal(TenantA, item.TenantId);
    }

    [Fact]
    public async Task Los_listados_no_devuelven_filas_de_otro_taller()
    {
        var connectionString = await NewDatabaseAsync();
        await using (var a = TestDbContext.Create(connectionString, TenantA))
        {
            a.Items.Add(NewItem("de A"));
            await a.SaveChangesAsync();
        }

        await using (var b = TestDbContext.Create(connectionString, TenantB))
        {
            b.Items.Add(NewItem("de B"));
            await b.SaveChangesAsync();
        }

        await using var readA = TestDbContext.Create(connectionString, TenantA);
        var names = await readA.Items.Select(i => i.Name).ToListAsync();

        Assert.Equal(["de A"], names);
    }

    [Fact]
    public async Task Sin_taller_en_la_sesion_no_se_ve_ninguna_fila()
    {
        var connectionString = await NewDatabaseAsync();
        await using (var a = TestDbContext.Create(connectionString, TenantA))
        {
            a.Items.Add(NewItem("de A"));
            await a.SaveChangesAsync();
        }

        await using var anonymous = TestDbContext.Create(connectionString, tenantId: null);

        Assert.Empty(await anonymous.Items.ToListAsync());
    }

    [Fact]
    public async Task Sin_taller_en_la_sesion_se_rechaza_escribir()
    {
        var connectionString = await NewDatabaseAsync();
        await using var anonymous = TestDbContext.Create(connectionString, tenantId: null);
        anonymous.Items.Add(NewItem("a"));

        await Assert.ThrowsAsync<TenantViolationException>(() => anonymous.SaveChangesAsync());
    }

    [Fact]
    public async Task Una_alta_con_el_taller_de_otro_se_rechaza()
    {
        var connectionString = await NewDatabaseAsync();
        await using var context = TestDbContext.Create(connectionString, TenantA);
        context.Items.Add(new TestItem("a", TestStatus.Active, 1m, DateTimeOffset.UtcNow, TenantB));

        await Assert.ThrowsAsync<TenantViolationException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Modificar_una_entidad_de_otro_taller_se_rechaza()
    {
        var connectionString = await NewDatabaseAsync();
        await using (var b = TestDbContext.Create(connectionString, TenantB))
        {
            b.Items.Add(NewItem("de B"));
            await b.SaveChangesAsync();
        }

        await using var a = TestDbContext.Create(connectionString, TenantA);
        var foreign = await a.Items.IgnoreQueryFilters().SingleAsync();
        foreign.Name = "tomada";

        await Assert.ThrowsAsync<TenantViolationException>(() => a.SaveChangesAsync());
    }

    [Fact]
    public async Task Cambiar_el_TenantId_de_una_entidad_propia_se_rechaza()
    {
        var connectionString = await NewDatabaseAsync();
        await using var context = TestDbContext.Create(connectionString, TenantA);
        var item = NewItem("a");
        context.Items.Add(item);
        await context.SaveChangesAsync();

        item.ChangeTenant(TenantB);

        await Assert.ThrowsAsync<TenantViolationException>(() => context.SaveChangesAsync());
    }
}
