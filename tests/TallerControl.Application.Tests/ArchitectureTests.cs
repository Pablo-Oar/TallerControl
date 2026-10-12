using System.Reflection;

namespace TallerControl.Application.Tests;

/// <summary>Regla de capas (doc 03): Application depende solo de Domain.</summary>
public class ArchitectureTests
{
    [Fact]
    public void Application_solo_referencia_a_Domain()
    {
        var application = Assembly.Load("TallerControl.Application");

        var referencias = application.GetReferencedAssemblies()
            .Select(a => a.Name)
            .Where(n => n is not null && n.StartsWith("TallerControl.", StringComparison.Ordinal))
            .ToList();

        Assert.All(referencias, n => Assert.Equal("TallerControl.Domain", n));
    }
}
