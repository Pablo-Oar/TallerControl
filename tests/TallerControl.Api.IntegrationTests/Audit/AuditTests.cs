using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TallerControl.Api.IntegrationTests.Persistence;
using TallerControl.Api.IntegrationTests.Tenancy;
using TallerControl.Domain.Audit;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Api.IntegrationTests.Audit;

/// <summary>REQ-19: auditoría de solo inserción con valores antes y después.</summary>
[Collection(PostgresCollection.Name)]
public sealed class AuditTests
{
    private static readonly Guid TenantA = Guid.CreateVersion7();

    private readonly PostgresFixture _postgres;

    public AuditTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    private async Task<string> NewTestModelDatabaseAsync()
    {
        var connectionString = await _postgres.CreateDatabaseAsync();
        await using var context = TestDbContext.Create(connectionString, TenantA);
        await context.Database.EnsureCreatedAsync();
        return connectionString;
    }

    private static async Task<List<AuditEvent>> EventsAsync(TestDbContext context) =>
        await context.Set<AuditEvent>().AsNoTracking().OrderBy(e => e.OccurredAt).ThenBy(e => e.Id).ToListAsync();

    private static JsonElement Changes(AuditEvent auditEvent) => JsonDocument.Parse(auditEvent.ChangesJson).RootElement;

    [Fact]
    public async Task Cambiar_un_precio_deja_el_valor_anterior_y_el_nuevo()
    {
        var connectionString = await NewTestModelDatabaseAsync();
        await using var context = TestDbContext.Create(connectionString, TenantA);
        var item = new TestItem("a", TestStatus.Active, 10m, DateTimeOffset.UtcNow);
        context.Items.Add(item);
        await context.SaveChangesAsync();

        item.Price = 12.5m;
        await context.SaveChangesAsync();

        var updated = (await EventsAsync(context)).Single(e => e.Action == "test_item.updated");
        var price = Changes(updated).GetProperty("price");
        Assert.Equal(10m, price[0].GetDecimal());
        Assert.Equal(12.5m, price[1].GetDecimal());
        Assert.Equal(item.Id, updated.EntityId);
        Assert.Equal("test_item", updated.EntityType);
        Assert.Equal(TenantA, updated.TenantId);
    }

    [Fact]
    public async Task Los_enums_se_auditan_como_texto_snake_case()
    {
        var connectionString = await NewTestModelDatabaseAsync();
        await using var context = TestDbContext.Create(connectionString, TenantA);
        var item = new TestItem("a", TestStatus.Active, 1m, DateTimeOffset.UtcNow);
        context.Items.Add(item);
        await context.SaveChangesAsync();

        item.Status = TestStatus.InProgress;
        await context.SaveChangesAsync();

        var updated = (await EventsAsync(context)).Single(e => e.Action == "test_item.updated");
        var status = Changes(updated).GetProperty("status");
        Assert.Equal("active", status[0].GetString());
        Assert.Equal("in_progress", status[1].GetString());
    }

    [Fact]
    public async Task El_alta_registra_los_valores_nuevos_y_omite_lo_sensible_y_la_version()
    {
        var connectionString = await NewTestModelDatabaseAsync();
        await using var context = TestDbContext.Create(connectionString, TenantA);
        var item = new TestItem("a", TestStatus.Active, 1m, DateTimeOffset.UtcNow) { Secret = "clave-super-secreta" };
        context.Items.Add(item);

        await context.SaveChangesAsync();

        var created = (await EventsAsync(context)).Single();
        Assert.Equal("test_item.created", created.Action);
        Assert.True(Changes(created).TryGetProperty("name", out _));
        Assert.DoesNotContain("secret", created.ChangesJson, StringComparison.Ordinal);
        Assert.DoesNotContain("clave-super-secreta", created.ChangesJson, StringComparison.Ordinal);
        Assert.DoesNotContain("version", created.ChangesJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_cambio_en_un_campo_sensible_no_se_filtra_a_la_auditoria()
    {
        var connectionString = await NewTestModelDatabaseAsync();
        await using var context = TestDbContext.Create(connectionString, TenantA);
        var item = new TestItem("a", TestStatus.Active, 1m, DateTimeOffset.UtcNow);
        context.Items.Add(item);
        await context.SaveChangesAsync();

        item.Secret = "otra-clave";
        item.Name = "b";
        await context.SaveChangesAsync();

        var updated = (await EventsAsync(context)).Single(e => e.Action == "test_item.updated");
        Assert.DoesNotContain("otra-clave", updated.ChangesJson, StringComparison.Ordinal);
        Assert.True(Changes(updated).TryGetProperty("name", out _));
    }

    [Fact]
    public async Task Una_modificacion_sin_cambios_reales_no_genera_evento()
    {
        var connectionString = await NewTestModelDatabaseAsync();
        await using var context = TestDbContext.Create(connectionString, TenantA);
        var item = new TestItem("a", TestStatus.Active, 1m, DateTimeOffset.UtcNow);
        context.Items.Add(item);
        await context.SaveChangesAsync();

        item.Name = "a";
        await context.SaveChangesAsync();

        Assert.Single(await EventsAsync(context));
    }

    [Fact]
    public async Task El_borrado_registra_los_valores_anteriores()
    {
        var connectionString = await NewTestModelDatabaseAsync();
        await using var context = TestDbContext.Create(connectionString, TenantA);
        var item = new TestItem("a", TestStatus.Active, 7m, DateTimeOffset.UtcNow);
        context.Items.Add(item);
        await context.SaveChangesAsync();

        context.Items.Remove(item);
        await context.SaveChangesAsync();

        var deleted = (await EventsAsync(context)).Single(e => e.Action == "test_item.deleted");
        var name = Changes(deleted).GetProperty("name");
        Assert.Equal("a", name[0].GetString());
        Assert.Equal(JsonValueKind.Null, name[1].ValueKind);
    }

    [Fact]
    public async Task El_evento_guarda_actor_ip_truncada_y_fecha_del_reloj()
    {
        var env = await TenancyDb.CreateAsync(_postgres);
        var actor = new TestActorContext
        {
            ActorType = AuditActorType.User,
            ActorId = Guid.CreateVersion7(),
            TruncatedIp = IPAddress.Parse("190.12.45.0"),
        };
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 10, 12, 15, 30, 0, TimeSpan.Zero));

        await using (var context = TenancyDb.CreateContext(env.ConnectionString, env.TenantId, actor, clock))
        {
            context.Add(new Branch(env.TenantId, "Centro"));
            await context.SaveChangesAsync();
        }

        await using var read = TenancyDb.CreateContext(env.ConnectionString, env.TenantId);
        var created = await read.Set<AuditEvent>().SingleAsync(e => e.EntityType == "branch" && e.OccurredAt == clock.GetUtcNow());

        Assert.Equal(AuditActorType.User, created.ActorType);
        Assert.Equal(actor.ActorId, created.ActorId);
        Assert.Equal(IPAddress.Parse("190.12.45.0"), created.IpTruncated);
        Assert.Equal("branch.created", created.Action);
    }

    [Fact]
    public async Task El_alta_del_taller_queda_auditada_con_el_actor_del_sistema()
    {
        var env = await TenancyDb.CreateAsync(_postgres);
        await using var context = TenancyDb.CreateContext(env.ConnectionString, env.TenantId);

        var actions = await context.Set<AuditEvent>().Select(e => e.Action).OrderBy(a => a).ToListAsync();

        Assert.Equal(["branch.created", "tenant.created", "tenant_settings.created"], actions);
        Assert.All(
            await context.Set<AuditEvent>().ToListAsync(),
            e => Assert.Equal(AuditActorType.System, e.ActorType));
    }

    [Fact]
    public async Task Un_cambio_que_falla_no_deja_evento_de_auditoria()
    {
        var env = await TenancyDb.CreateAsync(_postgres);
        await using var context = TenancyDb.CreateContext(env.ConnectionString, env.TenantId);
        var before = await context.Set<AuditEvent>().CountAsync();

        // Segunda sucursal principal: viola el índice único parcial.
        context.Add(new Branch(env.TenantId, "Otra", isMain: true));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        await using var fresh = TenancyDb.CreateContext(env.ConnectionString, env.TenantId);
        Assert.Equal(before, await fresh.Set<AuditEvent>().CountAsync());
    }

    [Fact]
    public async Task Un_taller_no_ve_la_auditoria_de_otro()
    {
        var env = await TenancyDb.CreateAsync(_postgres);
        var (otherTenant, _) = await TenancyDb.SeedTenantAsync(env.ConnectionString, "Otro taller");

        await using var other = TenancyDb.CreateContext(env.ConnectionString, otherTenant);
        var seen = await other.Set<AuditEvent>().ToListAsync();

        Assert.NotEmpty(seen);
        Assert.All(seen, e => Assert.Equal(otherTenant, e.TenantId));
    }

    [Theory]
    [InlineData("UPDATE audit_events SET action = 'x'")]
    [InlineData("DELETE FROM audit_events")]
    [InlineData("TRUNCATE audit_events")]
    public async Task La_base_rechaza_modificar_o_borrar_la_auditoria_incluso_al_dueno(string sql)
    {
        var env = await TenancyDb.CreateAsync(_postgres);

        var error = await Assert.ThrowsAsync<PostgresException>(
            () => TenancyDb.ExecuteAsync(env.ConnectionString, sql));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }
}
