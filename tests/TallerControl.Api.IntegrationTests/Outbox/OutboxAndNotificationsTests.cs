using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TallerControl.Api.IntegrationTests.Persistence;
using TallerControl.Api.IntegrationTests.Tenancy;
using TallerControl.Domain.Notifications;
using TallerControl.Domain.Outbox;
using TallerControl.Domain.Tenancy;
using TallerControl.Infrastructure.Outbox;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Api.IntegrationTests.Outbox;

[Collection(PostgresCollection.Name)]
public sealed class OutboxAndNotificationsTests
{
    private readonly PostgresFixture _postgres;

    public OutboxAndNotificationsTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    private static OutboxWriter WriterFor(AppDbContext context, Guid tenantId) =>
        new(context, TimeProvider.System, new TestTenantContext(tenantId));

    [Fact]
    public async Task El_mensaje_se_guarda_con_el_taller_el_tipo_y_el_payload()
    {
        var env = await TenancyDb.CreateAsync(_postgres);

        await using (var context = TenancyDb.CreateContext(env.ConnectionString, env.TenantId))
        {
            WriterFor(context, env.TenantId).Enqueue("email.quote_sent", new { to = "cliente@example.com", number = 7 });
            await context.SaveChangesAsync();
        }

        await using var read = TenancyDb.CreateContext(env.ConnectionString, env.TenantId);
        var message = await read.Set<OutboxMessage>().SingleAsync();

        Assert.Equal(env.TenantId, message.TenantId);
        Assert.Equal("email.quote_sent", message.Type);
        Assert.Null(message.ProcessedAt);
        Assert.Equal(0, message.Attempts);
        Assert.Equal(7, JsonDocument.Parse(message.PayloadJson).RootElement.GetProperty("number").GetInt32());
    }

    [Fact]
    public async Task El_mensaje_se_revierte_junto_con_el_cambio_de_negocio()
    {
        var env = await TenancyDb.CreateAsync(_postgres);

        await using (var context = TenancyDb.CreateContext(env.ConnectionString, env.TenantId))
        {
            WriterFor(context, env.TenantId).Enqueue("email.quote_sent", new { number = 1 });
            context.Add(new Branch(env.TenantId, "Otra", isMain: true)); // viola el índice único parcial
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        await using var read = TenancyDb.CreateContext(env.ConnectionString, env.TenantId);
        Assert.Empty(await read.Set<OutboxMessage>().ToListAsync());
    }

    [Fact]
    public async Task Un_mensaje_de_otro_taller_se_rechaza_en_una_sesion_con_taller()
    {
        var env = await TenancyDb.CreateAsync(_postgres);
        var (otherTenant, _) = await TenancyDb.SeedTenantAsync(env.ConnectionString, "Otro taller");
        await using var context = TenancyDb.CreateContext(env.ConnectionString, env.TenantId);

        context.Add(new OutboxMessage("email.test", "{}", DateTimeOffset.UtcNow, otherTenant));

        await Assert.ThrowsAsync<TenantViolationException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Un_proceso_sin_taller_puede_encolar_y_marcar_procesado()
    {
        var env = await TenancyDb.CreateAsync(_postgres);

        await using (var system = TenancyDb.CreateContext(env.ConnectionString, tenantId: null))
        {
            system.Add(new OutboxMessage("email.reset_password", "{}", DateTimeOffset.UtcNow));
            await system.SaveChangesAsync();
        }

        await using var worker = TenancyDb.CreateContext(env.ConnectionString, tenantId: null);
        var pending = await worker.Set<OutboxMessage>().SingleAsync(m => m.ProcessedAt == null);
        pending.MarkProcessed(DateTimeOffset.UtcNow);
        await worker.SaveChangesAsync();

        await using var read = TenancyDb.CreateContext(env.ConnectionString, tenantId: null);
        Assert.NotNull((await read.Set<OutboxMessage>().SingleAsync()).ProcessedAt);
    }

    [Fact]
    public async Task Los_avisos_son_del_taller_y_pueden_ser_para_todos()
    {
        var env = await TenancyDb.CreateAsync(_postgres);
        var (otherTenant, _) = await TenancyDb.SeedTenantAsync(env.ConnectionString, "Otro taller");

        await using (var context = TenancyDb.CreateContext(env.ConnectionString, env.TenantId))
        {
            context.Add(new Notification(env.TenantId, "quote.approved", "Presupuesto aprobado", DateTimeOffset.UtcNow));
            await context.SaveChangesAsync();
        }

        await using var mine = TenancyDb.CreateContext(env.ConnectionString, env.TenantId);
        var notification = await mine.Set<Notification>().SingleAsync();
        Assert.Null(notification.MembershipId);
        Assert.Null(notification.ReadAt);

        await using var other = TenancyDb.CreateContext(env.ConnectionString, otherTenant);
        Assert.Empty(await other.Set<Notification>().ToListAsync());
    }
}
