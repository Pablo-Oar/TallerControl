using TallerControl.Api.IntegrationTests.Persistence;
using TallerControl.Domain.Tenancy;
using TallerControl.Infrastructure.Tenancy;

namespace TallerControl.Api.IntegrationTests.Tenancy;

/// <summary>E1: numeración concurrente sin huecos ni duplicados (doc 09, E1).</summary>
[Collection(PostgresCollection.Name)]
public sealed class DocumentNumberGeneratorTests
{
    private readonly PostgresFixture _postgres;

    public DocumentNumberGeneratorTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    private static DocumentNumberGenerator CreateGenerator(
        Infrastructure.Persistence.AppDbContext db,
        Guid? tenantId,
        Guid? branchId) =>
        new(db, new TestTenantContext(tenantId), new TestBranchContext(branchId));

    [Fact]
    public async Task Entrega_numeros_correlativos_desde_el_uno()
    {
        var env = await TenancyDb.CreateAsync(_postgres);
        await using var db = TenancyDb.CreateContext(env.ConnectionString, env.TenantId);
        var generator = CreateGenerator(db, env.TenantId, env.BranchId);

        await using var tx = await db.Database.BeginTransactionAsync();
        var numbers = new[]
        {
            await generator.NextAsync(DocumentKind.Quote),
            await generator.NextAsync(DocumentKind.Quote),
            await generator.NextAsync(DocumentKind.Quote),
        };
        await tx.CommitAsync();

        Assert.Equal([1, 2, 3], numbers);
    }

    [Fact]
    public async Task Cada_tipo_y_cada_sucursal_tiene_su_propia_numeracion()
    {
        var env = await TenancyDb.CreateAsync(_postgres);

        Guid secondBranch;
        await using (var setup = TenancyDb.CreateContext(env.ConnectionString, env.TenantId))
        {
            var branch = new Branch(env.TenantId, "Sucursal 2");
            setup.Add(branch);
            await setup.SaveChangesAsync();
            secondBranch = branch.Id;
        }

        await using var db = TenancyDb.CreateContext(env.ConnectionString, env.TenantId);
        await using var tx = await db.Database.BeginTransactionAsync();
        var main = CreateGenerator(db, env.TenantId, env.BranchId);
        var second = CreateGenerator(db, env.TenantId, secondBranch);

        Assert.Equal(1, await main.NextAsync(DocumentKind.Quote));
        Assert.Equal(2, await main.NextAsync(DocumentKind.Quote));
        Assert.Equal(1, await main.NextAsync(DocumentKind.WorkOrder));
        Assert.Equal(1, await second.NextAsync(DocumentKind.Quote));
        Assert.Equal(1, await main.NextAsync(DocumentKind.Payment));
    }

    [Fact]
    public async Task Un_rollback_devuelve_el_numero_y_no_deja_huecos()
    {
        var env = await TenancyDb.CreateAsync(_postgres);

        await using (var failing = TenancyDb.CreateContext(env.ConnectionString, env.TenantId))
        {
            await using var tx = await failing.Database.BeginTransactionAsync();
            var generator = CreateGenerator(failing, env.TenantId, env.BranchId);
            Assert.Equal(1, await generator.NextAsync(DocumentKind.Quote));
            await tx.RollbackAsync();
        }

        await using var db = TenancyDb.CreateContext(env.ConnectionString, env.TenantId);
        await using var next = await db.Database.BeginTransactionAsync();
        var again = CreateGenerator(db, env.TenantId, env.BranchId);

        Assert.Equal(1, await again.NextAsync(DocumentKind.Quote));
    }

    [Fact]
    public async Task Pedidos_concurrentes_no_repiten_ni_saltean_numeros()
    {
        const int parallel = 40;
        var env = await TenancyDb.CreateAsync(_postgres);

        var numbers = await Task.WhenAll(Enumerable.Range(0, parallel).Select(async i =>
        {
            await using var db = TenancyDb.CreateContext(env.ConnectionString, env.TenantId);
            await using var tx = await db.Database.BeginTransactionAsync();
            var generator = CreateGenerator(db, env.TenantId, env.BranchId);

            var number = await generator.NextAsync(DocumentKind.WorkOrder);

            // Retiene el bloqueo un instante para forzar la contención real entre transacciones.
            await Task.Delay(5 + (i % 7));
            await tx.CommitAsync();
            return number;
        }));

        Assert.Equal(Enumerable.Range(1, parallel), numbers.OrderBy(n => n));
    }

    [Fact]
    public async Task Fuera_de_una_transaccion_se_rechaza()
    {
        var env = await TenancyDb.CreateAsync(_postgres);
        await using var db = TenancyDb.CreateContext(env.ConnectionString, env.TenantId);
        var generator = CreateGenerator(db, env.TenantId, env.BranchId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => generator.NextAsync(DocumentKind.Quote));
    }

    [Fact]
    public async Task Sin_taller_o_sin_sucursal_en_la_sesion_se_rechaza()
    {
        var env = await TenancyDb.CreateAsync(_postgres);
        await using var db = TenancyDb.CreateContext(env.ConnectionString, env.TenantId);
        await using var tx = await db.Database.BeginTransactionAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateGenerator(db, null, env.BranchId).NextAsync(DocumentKind.Quote));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateGenerator(db, env.TenantId, null).NextAsync(DocumentKind.Quote));
    }
}
