using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TallerControl.Application.Common;
using TallerControl.Domain.Common;

namespace TallerControl.Infrastructure.Persistence;

/// <summary>
/// Contexto único de persistencia. Fija las convenciones del modelo (doc 04 §1):
/// <list type="bullet">
/// <item>Tablas y columnas en <c>snake_case</c> (paquete EFCore.NamingConventions).</item>
/// <item>PK <c>uuid</c> generada en la aplicación (UUIDv7, ver <see cref="Entity"/>).</item>
/// <item>Dinero <c>numeric(14,2)</c> por defecto; cantidades y tasas se declaran por propiedad.</item>
/// <item>Fechas <c>timestamptz</c> en UTC: las entidades usan <see cref="DateTimeOffset"/> y Npgsql
/// rechaza valores con offset distinto de cero.</item>
/// <item>Enums como <c>text</c> en <c>snake_case</c> más un <c>CHECK</c> por columna.</item>
/// <item><see cref="ITenantOwned"/>: filtro global por taller y validación en <c>SaveChanges</c> (REQ-01).</item>
/// <item><see cref="IVersioned"/>: concurrencia optimista con <c>xmin</c>.</item>
/// </list>
/// </summary>
public class AppDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    protected AppDbContext(DbContextOptions options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    // Lo lee el filtro global; EF lo evalúa por instancia de contexto.
    private Guid? CurrentTenantId => _tenantContext.TenantId;

    /// <summary>Punto de extensión para registrar las entidades. Por defecto, las configuraciones del ensamblado.</summary>
    protected virtual void ConfigureEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.HasPostgresExtension("pg_trgm");

        ConfigureEntities(modelBuilder);
        ApplyConventions(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(14, 2);
    }

    private void ApplyConventions(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
        {
            ApplyEnumConventions(entityType);

            if (typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            {
                typeof(AppDbContext)
                    .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(this, [modelBuilder]);
            }

            if (typeof(IVersioned).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(IVersioned.Version))
                    .IsRowVersion();
            }
        }
    }

    private static void ApplyEnumConventions(IMutableEntityType entityType)
    {
        foreach (var property in entityType.GetProperties().ToList())
        {
            var enumType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            if (!enumType.IsEnum)
            {
                continue;
            }

            var converterType = typeof(SnakeCaseEnumConverter<>).MakeGenericType(enumType);
            property.SetValueConverter((ValueConverter)Activator.CreateInstance(converterType)!);

            var mapType = typeof(EnumTextMap<>).MakeGenericType(enumType);
            var values = (IEnumerable<string>)mapType
                .GetProperty("Values", BindingFlags.Public | BindingFlags.Static)!
                .GetValue(null)!;

            var table = entityType.GetTableName();
            var column = property.GetColumnName();
            if (table is null || column is null)
            {
                continue;
            }

            var allowed = string.Join(", ", values.OrderBy(v => v, StringComparer.Ordinal).Select(v => $"'{v}'"));
            entityType.AddCheckConstraint($"ck_{table}_{column}", $"\"{column}\" IN ({allowed})");
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(
            e => EF.Property<Guid>(e, nameof(ITenantOwned.TenantId)) == CurrentTenantId);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceTenantBoundaries();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnforceTenantBoundaries();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// REQ-01 CA(3): las altas toman el taller de la sesión; cualquier modificación de una entidad
    /// de otro taller, o del propio <c>TenantId</c>, se rechaza antes de llegar a la base.
    /// </summary>
    private void EnforceTenantBoundaries()
    {
        var tenantId = _tenantContext.TenantId;

        foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            if (tenantId is null)
            {
                throw new TenantViolationException(
                    $"No hay taller en la sesión: no se puede escribir {entry.Metadata.ClrType.Name}.");
            }

            var property = entry.Property(nameof(ITenantOwned.TenantId));
            var current = (Guid)property.CurrentValue!;

            if (entry.State == EntityState.Added)
            {
                if (current == Guid.Empty)
                {
                    property.CurrentValue = tenantId.Value;
                    continue;
                }
            }
            else if (property.IsModified)
            {
                throw new TenantViolationException(
                    $"No se permite cambiar el taller de {entry.Metadata.ClrType.Name}.");
            }

            if (current != tenantId.Value)
            {
                throw new TenantViolationException(
                    $"{entry.Metadata.ClrType.Name} pertenece a otro taller.");
            }
        }
    }
}
