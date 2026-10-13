using Microsoft.EntityFrameworkCore;
using Npgsql;
using TallerControl.Api.IntegrationTests.Persistence;
using TallerControl.Application.Common;
using TallerControl.Domain.Tenancy;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Api.IntegrationTests.Tenancy;

public sealed class TestBranchContext : IBranchContext
{
    public TestBranchContext(Guid? branchId = null)
    {
        BranchId = branchId;
    }

    public Guid? BranchId { get; }
}

/// <summary>Una base migrada con un taller, su sucursal principal y su configuración.</summary>
public sealed class TenancyDb
{
    private TenancyDb(string connectionString, Guid tenantId, Guid branchId)
    {
        ConnectionString = connectionString;
        TenantId = tenantId;
        BranchId = branchId;
    }

    public string ConnectionString { get; }

    public Guid TenantId { get; }

    public Guid BranchId { get; }

    public static async Task<TenancyDb> CreateAsync(PostgresFixture postgres)
    {
        var connectionString = await postgres.CreateDatabaseAsync();

        await using (var migrator = CreateContext(connectionString, tenantId: null))
        {
            await migrator.Database.MigrateAsync();
        }

        var (tenantId, branchId) = await SeedTenantAsync(connectionString, "Taller de prueba");
        return new TenancyDb(connectionString, tenantId, branchId);
    }

    public static AppDbContext CreateContext(string connectionString, Guid? tenantId)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        builder.UseTallerControlNpgsql(connectionString);
        return new AppDbContext(builder.Options, new TestTenantContext(tenantId));
    }

    public static async Task<(Guid TenantId, Guid BranchId)> SeedTenantAsync(string connectionString, string name)
    {
        var tenant = new Tenant(name, DateTimeOffset.UtcNow);

        await using (var platform = CreateContext(connectionString, tenantId: null))
        {
            platform.Add(tenant);
            await platform.SaveChangesAsync();
        }

        var branch = Branch.CreateMain(tenant.Id);
        await using var scoped = CreateContext(connectionString, tenant.Id);
        scoped.Add(branch);
        scoped.Add(new TenantSettings(tenant.Id));
        await scoped.SaveChangesAsync();

        return (tenant.Id, branch.Id);
    }

    public static async Task<int> ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteNonQueryAsync();
    }
}
