using TallerControl.Domain.Tenancy;

namespace TallerControl.Domain.Tests;

public class TenancyTests
{
    [Fact]
    public void Un_taller_nuevo_esta_activo_y_con_la_zona_horaria_por_defecto()
    {
        var tenant = new Tenant("  Taller Pérez ", DateTimeOffset.UtcNow);

        Assert.Equal("Taller Pérez", tenant.Name);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Equal(Tenant.DefaultTimeZone, tenant.TimeZone);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void El_nombre_del_taller_no_puede_estar_vacio(string name)
    {
        Assert.Throws<ArgumentException>(() => new Tenant(name, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void El_nombre_del_taller_no_puede_pasar_de_120_caracteres()
    {
        Assert.Throws<ArgumentException>(() => new Tenant(new string('a', 121), DateTimeOffset.UtcNow));
        _ = new Tenant(new string('a', 120), DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Un_taller_se_suspende_y_se_reactiva_pero_no_se_borra()
    {
        var tenant = new Tenant("Taller", DateTimeOffset.UtcNow);

        tenant.Suspend();
        Assert.Equal(TenantStatus.Suspended, tenant.Status);

        tenant.Activate();
        Assert.Equal(TenantStatus.Active, tenant.Status);
    }

    [Fact]
    public void La_sucursal_principal_se_llama_Principal_y_pertenece_al_taller()
    {
        var tenantId = Guid.CreateVersion7();

        var branch = Branch.CreateMain(tenantId);

        Assert.True(branch.IsMain);
        Assert.Equal("Principal", branch.Name);
        Assert.Equal(tenantId, branch.TenantId);
    }

    [Fact]
    public void Una_sucursal_exige_un_taller()
    {
        Assert.Throws<ArgumentException>(() => new Branch(Guid.Empty, "Sucursal"));
    }

    [Fact]
    public void La_configuracion_nueva_trae_los_valores_por_defecto_del_plan()
    {
        var settings = new TenantSettings(Guid.CreateVersion7());

        Assert.Equal(21.00m, settings.DefaultVat);
        Assert.Equal(15, settings.QuoteValidityDays);
        Assert.Equal("54", settings.PhoneCountryCode);
        Assert.False(settings.QrPlateLookupEnabled);
        Assert.Contains(settings.DefaultVat, TenantSettings.AllowedVatRates);
    }
}
