using TallerControl.Domain.Users;

namespace TallerControl.Domain.Tests.Users;

public class PermissionTests
{
    [Fact]
    public void El_owner_tiene_todos_los_permisos_definidos()
    {
        Assert.Contains(Permission.SettingsManage, Permission.ForOwner);
        Assert.Contains(Permission.UsersManage, Permission.ForOwner);
        Assert.Contains(Permission.PaymentsVoid, Permission.ForOwner);
        Assert.Contains(Permission.AuditRead, Permission.ForOwner);
    }

    [Fact]
    public void El_administrativo_no_puede_gestionar_usuarios_ni_configuración()
    {
        Assert.DoesNotContain(Permission.SettingsManage, Permission.ForAdminStaff);
        Assert.DoesNotContain(Permission.UsersManage, Permission.ForAdminStaff);
    }

    [Fact]
    public void El_mecánico_no_ve_pagos_ni_costos()
    {
        Assert.DoesNotContain(Permission.PaymentsRead, Permission.ForMechanic);
        Assert.DoesNotContain(Permission.CostsView, Permission.ForMechanic);
        Assert.DoesNotContain(Permission.PricesView, Permission.ForMechanic);
    }

    [Fact]
    public void Los_permisos_finos_del_mecánico_son_un_subconjunto_del_owner()
    {
        Assert.True(Permission.ForOwner.IsSupersetOf(Permission.MechanicFineGrained));
    }

    [Fact]
    public void BaseFor_devuelve_los_permisos_del_rol()
    {
        Assert.Same(Permission.ForOwner, Permission.BaseFor(TenantRole.Owner));
        Assert.Same(Permission.ForAdminStaff, Permission.BaseFor(TenantRole.AdminStaff));
        Assert.Same(Permission.ForMechanic, Permission.BaseFor(TenantRole.Mechanic));
    }
}
