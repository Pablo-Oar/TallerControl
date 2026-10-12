using System.Reflection;

namespace TallerControl.Domain.Tests;

/// <summary>Regla de capas (doc 03): Domain no referencia a ningún otro proyecto de la solución.</summary>
public class ArchitectureTests
{
    [Fact]
    public void Domain_no_referencia_otros_proyectos_de_TallerControl()
    {
        var domain = Assembly.Load("TallerControl.Domain");

        var referencias = domain.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(n => n is not null && n.StartsWith("TallerControl.", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(referencias);
    }
}
