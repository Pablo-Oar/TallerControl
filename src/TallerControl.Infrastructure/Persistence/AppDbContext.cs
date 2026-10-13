using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TallerControl.Application.Common;
using TallerControl.Domain.Audit;
using TallerControl.Domain.Common;
using TallerControl.Domain.Tenancy;

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
/// <item><see cref="IOptionalTenantOwned"/>: eventos de sistema; el taller solo ve los suyos.</item>
/// <item><see cref="IVersioned"/>: concurrencia optimista con <c>xmin</c>.</item>
/// <item><see cref="IAudited"/>: cada alta, cambio o borrado genera un <see cref="AuditEvent"/> en la
/// misma transacción (REQ-19).</item>
/// </list>
/// </summary>
public class AppDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;
    private readonly IActorContext _actorContext;
    private readonly TimeProvider _clock;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ITenantContext tenantContext,
        IActorContext actorContext,
        TimeProvider clock)
        : base(options)
    {
        _tenantContext = tenantContext;
        _actorContext = actorContext;
        _clock = clock;
    }

    protected AppDbContext(
        DbContextOptions options,
        ITenantContext tenantContext,
        IActorContext actorContext,
        TimeProvider clock)
        : base(options)
    {
        _tenantContext = tenantContext;
        _actorContext = actorContext;
        _clock = clock;
    }

    // Lo leen los filtros globales; EF los evalúa por instancia de contexto.
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

            var isTenantOwned = typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType);
            var isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType);

            if (isTenantOwned && isSoftDeletable)
            {
                // HasQueryFilter acepta solo uno por tipo — hay que combinarlos.
                InvokeFilter(nameof(ApplyTenantAndSoftDeleteFilter), entityType.ClrType, modelBuilder);
            }
            else if (isTenantOwned)
            {
                InvokeFilter(nameof(ApplyTenantFilter), entityType.ClrType, modelBuilder);
            }
            else if (typeof(IOptionalTenantOwned).IsAssignableFrom(entityType.ClrType))
            {
                InvokeFilter(nameof(ApplyOptionalTenantFilter), entityType.ClrType, modelBuilder);
            }
            else if (isSoftDeletable)
            {
                InvokeFilter(nameof(ApplySoftDeleteFilter), entityType.ClrType, modelBuilder);
            }

            if (typeof(IVersioned).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(IVersioned.Version))
                    .IsRowVersion();
            }
        }
    }

    private void InvokeFilter(string method, Type entityType, ModelBuilder modelBuilder)
    {
        typeof(AppDbContext)
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(entityType)
            .Invoke(this, [modelBuilder]);
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

    private void ApplyOptionalTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class
    {
        // Las filas globales (tenant_id nulo) no las ve ningún taller.
        modelBuilder.Entity<TEntity>().HasQueryFilter(
            e => EF.Property<Guid?>(e, nameof(IOptionalTenantOwned.TenantId)) == CurrentTenantId);
    }

    private void ApplyTenantAndSoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned, ISoftDeletable
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(
            e => EF.Property<Guid>(e, nameof(ITenantOwned.TenantId)) == CurrentTenantId
              && e.DeletedAt == null);
    }

    private static void ApplySoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ISoftDeletable
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.DeletedAt == null);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        PrepareChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void PrepareChanges()
    {
        ChangeTracker.DetectChanges();
        GuardSoftDeletableHardDelete();
        SetAuditTimestamps();
        EnforceTenantBoundaries();
        CaptureAudit();
    }

    /// <summary>
    /// Bloquea el borrado físico de entidades con borrado lógico. Siempre usar el método
    /// <c>Delete()</c> de la entidad y guardar con <c>SaveChanges</c>.
    /// </summary>
    private void GuardSoftDeletableHardDelete()
    {
        foreach (var entry in ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State == EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    $"No se puede borrar físicamente '{entry.Metadata.ClrType.Name}'. " +
                    "Usar el método Delete() de la entidad (borrado lógico).");
            }
        }
    }

    /// <summary>
    /// Setea <c>created_at</c>/<c>created_by</c> en altas y <c>updated_at</c>/<c>updated_by</c>
    /// en modificaciones, para todas las entidades <see cref="IAuditable"/> (doc 04 §1).
    /// </summary>
    private void SetAuditTimestamps()
    {
        var now = _clock.GetUtcNow();
        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(IAuditable.CreatedAt)).CurrentValue = now;
                entry.Property(nameof(IAuditable.CreatedBy)).CurrentValue = _actorContext.ActorId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(IAuditable.UpdatedAt)).CurrentValue = now;
                entry.Property(nameof(IAuditable.UpdatedBy)).CurrentValue = _actorContext.ActorId;
            }
        }
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

            var property = entry.Property(nameof(ITenantOwned.TenantId));
            var current = (Guid)property.CurrentValue!;

            if (tenantId is null)
            {
                // Sin sesión: operaciones de plataforma (p. ej. AcceptInvitationHandler) donde el
                // TenantId ya está fijado por código interno (invitación, semilla). Solo se permite
                // si el TenantId es explícito y no se está modificando.
                if (current == Guid.Empty)
                {
                    throw new TenantViolationException(
                        $"No hay taller en la sesión: no se puede escribir {entry.Metadata.ClrType.Name}.");
                }

                if (property.IsModified)
                {
                    throw new TenantViolationException(
                        $"No se permite cambiar el taller de {entry.Metadata.ClrType.Name}.");
                }

                continue;
            }

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

        EnforceOptionalTenantBoundaries(tenantId);
    }

    /// <summary>
    /// Entidades de sistema (<see cref="IOptionalTenantOwned"/>). Con un taller en la sesión solo se
    /// escribe en ese taller. Sin taller (procesos del sistema, altas previas a la sesión) se admite
    /// cualquier valor, y un alta sin taller toma el de la sesión cuando existe.
    /// </summary>
    private void EnforceOptionalTenantBoundaries(Guid? tenantId)
    {
        foreach (var entry in ChangeTracker.Entries<IOptionalTenantOwned>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var property = entry.Property(nameof(IOptionalTenantOwned.TenantId));
            var current = (Guid?)property.CurrentValue;

            if (entry.State == EntityState.Added && current is null)
            {
                property.CurrentValue = tenantId;
                continue;
            }

            if (tenantId is not null && current != tenantId)
            {
                throw new TenantViolationException(
                    $"{entry.Metadata.ClrType.Name} pertenece a otro taller o es un evento global.");
            }
        }
    }

    /// <summary>
    /// REQ-19: por cada entidad <see cref="IAudited"/> alta, modificada o borrada agrega un
    /// <see cref="AuditEvent"/> con <c>{campo: [antes, después]}</c>. Va en el mismo SaveChanges,
    /// así que se confirma o se revierte con el cambio.
    /// </summary>
    private void CaptureAudit()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is IAudited
                && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (entries.Count == 0)
        {
            return;
        }

        var now = _clock.GetUtcNow();

        foreach (var entry in entries)
        {
            var changes = BuildChanges(entry);
            if (changes.Count == 0)
            {
                continue;
            }

            var primaryKey = entry.Metadata.FindPrimaryKey()!.Properties[0];
            var entityId = (Guid)entry.Property(primaryKey.Name).CurrentValue!;

            var verb = entry.State switch
            {
                EntityState.Added => "created",
                EntityState.Deleted => "deleted",
                _ => "updated",
            };
            var entityName = EnumText.ToSnakeCase(entry.Metadata.ClrType.Name);

            Set<AuditEvent>().Add(new AuditEvent(
                ResolveAuditTenant(entry.Entity, entityId),
                _actorContext.ActorType,
                _actorContext.ActorId,
                entityName,
                entityId,
                $"{entityName}.{verb}",
                JsonSerializer.Serialize(changes),
                _actorContext.TruncatedIp,
                now));
        }
    }

    private Guid? ResolveAuditTenant(object entity, Guid entityId) => entity switch
    {
        ITenantOwned owned => owned.TenantId,
        Tenant => entityId,
        _ => _tenantContext.TenantId,
    };

    private static SortedDictionary<string, object?[]> BuildChanges(EntityEntry entry)
    {
        var changes = new SortedDictionary<string, object?[]>(StringComparer.Ordinal);

        foreach (var property in entry.Properties)
        {
            var metadata = property.Metadata;
            if (metadata.Name == nameof(IVersioned.Version)
                || metadata.PropertyInfo?.IsDefined(typeof(AuditIgnoreAttribute), inherit: true) == true)
            {
                continue;
            }

            var column = metadata.GetColumnName();
            switch (entry.State)
            {
                case EntityState.Added when property.CurrentValue is not null:
                    changes[column] = [null, ToAuditValue(property.CurrentValue)];
                    break;

                case EntityState.Deleted when property.OriginalValue is not null:
                    changes[column] = [ToAuditValue(property.OriginalValue), null];
                    break;

                case EntityState.Modified when property.IsModified:
                    var before = ToAuditValue(property.OriginalValue);
                    var after = ToAuditValue(property.CurrentValue);
                    if (!Equals(before, after))
                    {
                        changes[column] = [before, after];
                    }

                    break;
            }
        }

        return changes;
    }

    private static object? ToAuditValue(object? value) => value switch
    {
        null => null,
        Enum e => EnumText.ToSnakeCase(e.ToString()),
        _ => value,
    };
}
