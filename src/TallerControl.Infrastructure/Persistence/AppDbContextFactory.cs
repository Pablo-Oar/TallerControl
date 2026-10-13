using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TallerControl.Infrastructure.Tenancy;

namespace TallerControl.Infrastructure.Persistence;

/// <summary>
/// Solo para <c>dotnet ef</c> (migraciones y bundle). La cadena sale de la variable
/// <c>ConnectionStrings__Default</c>; sin ella se usa una de diseño que no se conecta.
/// </summary>
internal sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=127.0.0.1;Database=tallercontrol_design;Username=design";

        var builder = new DbContextOptionsBuilder<AppDbContext>();
        builder.UseTallerControlNpgsql(connectionString);

        return new AppDbContext(builder.Options, new NoTenantContext(), new SystemActorContext(), TimeProvider.System);
    }
}
