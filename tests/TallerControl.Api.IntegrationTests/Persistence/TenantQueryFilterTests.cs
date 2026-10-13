using Microsoft.EntityFrameworkCore;
using TallerControl.Domain.Common;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Api.IntegrationTests.Persistence;

/// <summary>
/// T-ISO-02: toda entidad con <see cref="ITenantOwned"/> tiene que llevar el filtro global de
/// taller activo; si alguien lo olvida en una entidad nueva, este test lo detecta solo.
/// No necesita Postgres: el modelo de EF se construye sin abrir conexión (doc 08 §3).
/// </summary>
public sealed class TenantQueryFilterTests
{
    private static AppDbContext BuildModel()
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        builder.UseTallerControlNpgsql("Host=127.0.0.1;Database=tallercontrol_model_check;Username=design");
        return new AppDbContext(builder.Options, new TestTenantContext(), new TestActorContext(), TimeProvider.System);
    }

    [Fact]
    public void Toda_entidad_ITenantOwned_tiene_filtro_global_de_taller()
    {
        using var context = BuildModel();

        var tenantOwnedTypes = context.Model.GetEntityTypes()
            .Where(e => typeof(ITenantOwned).IsAssignableFrom(e.ClrType))
            .ToList();

        // Si esto falla, no hay nada que aislar: revisá que el modelo se esté construyendo bien.
        Assert.NotEmpty(tenantOwnedTypes);

        foreach (var entityType in tenantOwnedTypes)
        {
            Assert.True(
                entityType.GetDeclaredQueryFilters().Any(),
                $"{entityType.ClrType.Name} implementa ITenantOwned pero no tiene HasQueryFilter.");
        }
    }

    [Fact]
    public void Toda_entidad_IOptionalTenantOwned_tiene_filtro_global_de_taller()
    {
        using var context = BuildModel();

        var optionalTenantOwnedTypes = context.Model.GetEntityTypes()
            .Where(e => typeof(IOptionalTenantOwned).IsAssignableFrom(e.ClrType))
            .ToList();

        Assert.NotEmpty(optionalTenantOwnedTypes);

        foreach (var entityType in optionalTenantOwnedTypes)
        {
            Assert.True(
                entityType.GetDeclaredQueryFilters().Any(),
                $"{entityType.ClrType.Name} implementa IOptionalTenantOwned pero no tiene HasQueryFilter.");
        }
    }
}
