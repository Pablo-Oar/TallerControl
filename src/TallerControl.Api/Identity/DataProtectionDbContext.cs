using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace TallerControl.Api.Identity;

/// <summary>
/// Contexto mínimo para persistir las claves de Data Protection en la BD (doc E9 bloque 1).
/// Tabla separada del AppDbContext para evitar dep transitiva vulnerable en Infrastructure.
/// La tabla <c>data_protection_keys</c> es creada por la migración DataProtectionKeys.
/// </summary>
public class DataProtectionDbContext : DbContext, IDataProtectionKeyContext
{
    public DataProtectionDbContext(DbContextOptions<DataProtectionDbContext> options)
        : base(options)
    {
    }

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DataProtectionKey>(b =>
        {
            b.ToTable("data_protection_keys");
            b.Property(k => k.Id).HasColumnName("id").ValueGeneratedOnAdd();
            b.Property(k => k.FriendlyName).HasColumnName("friendly_name").HasMaxLength(900);
            b.Property(k => k.Xml).HasColumnName("xml");
        });
    }
}
