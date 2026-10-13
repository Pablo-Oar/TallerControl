using System.Reflection;
using TallerControl.Domain.Common;

namespace TallerControl.Domain.Tests;

public class ConventionsTests
{
    private sealed class Sample : Entity
    {
        public Guid IdValue => Id;
    }

    [Fact]
    public void Las_entidades_generan_un_UUIDv7()
    {
        var entity = new Sample();

        Assert.Equal(7, entity.IdValue.Version);
    }

    [Fact]
    public void Los_ids_nuevos_son_ordenables_por_tiempo()
    {
        var first = new Sample().IdValue;
        Thread.Sleep(5); // el UUIDv7 solo ordena entre milisegundos distintos
        var second = new Sample().IdValue;

        // PostgreSQL ordena los uuid byte a byte (orden RFC), igual que su texto hexadecimal;
        // Guid.CompareTo de .NET compara por campos de la estructura y no sirve para esto.
        Assert.True(string.CompareOrdinal(first.ToString(), second.ToString()) < 0);
    }

    /// <summary>
    /// REQ-01: una entidad con <c>TenantId</c> debe declararse <see cref="ITenantOwned"/>; sin eso
    /// no recibiría el filtro global ni la validación en SaveChanges.
    /// </summary>
    [Fact]
    public void Toda_entidad_con_TenantId_implementa_ITenantOwned()
    {
        var domain = typeof(Entity).Assembly;

        var offenders = domain.GetTypes()
            .Where(t => t.GetProperty("TenantId", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic) is not null)
            .Where(t => !typeof(ITenantOwned).IsAssignableFrom(t))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(offenders);
    }
}
