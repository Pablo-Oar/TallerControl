using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Common;
using TallerControl.Domain.Common;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Api.IntegrationTests.Persistence;

public enum TestStatus
{
    Active,
    InProgress,
}

/// <summary>Entidad de prueba que ejercita todas las convenciones: taller, xmin, enum, dinero y fecha.</summary>
public sealed class TestItem : Entity, ITenantOwned, IVersioned
{
    private TestItem()
    {
    }

    public TestItem(string name, TestStatus status, decimal price, DateTimeOffset occurredAt, Guid tenantId = default)
    {
        Name = name;
        Status = status;
        Price = price;
        OccurredAt = occurredAt;
        TenantId = tenantId;
    }

    public Guid TenantId { get; private set; }

    public string Name { get; set; } = string.Empty;

    public TestStatus Status { get; set; }

    public decimal Price { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public uint Version { get; private set; }

    public void ChangeTenant(Guid tenantId) => TenantId = tenantId;
}

public sealed class TestTenantContext : ITenantContext
{
    public TestTenantContext(Guid? tenantId = null)
    {
        TenantId = tenantId;
    }

    public Guid? TenantId { get; }
}

public sealed class TestDbContext : AppDbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    public DbSet<TestItem> Items => Set<TestItem>();

    protected override void ConfigureEntities(ModelBuilder modelBuilder)
    {
        base.ConfigureEntities(modelBuilder);
        modelBuilder.Entity<TestItem>().ToTable("test_items");
    }

    public static TestDbContext Create(string connectionString, Guid? tenantId = null)
    {
        var builder = new DbContextOptionsBuilder<TestDbContext>();
        builder.UseTallerControlNpgsql(connectionString);
        return new TestDbContext(builder.Options, new TestTenantContext(tenantId));
    }
}
