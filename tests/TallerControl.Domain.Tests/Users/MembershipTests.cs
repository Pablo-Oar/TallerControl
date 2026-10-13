using TallerControl.Domain.Users;

namespace TallerControl.Domain.Tests.Users;

public class MembershipTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid UserId = Guid.CreateVersion7();

    [Fact]
    public void Una_membresía_nueva_queda_activa_con_los_permisos_base_del_rol()
    {
        var membership = new Membership(TenantId, UserId, TenantRole.AdminStaff);

        Assert.Equal(TenantRole.AdminStaff, membership.Role);
        Assert.Equal(MembershipStatus.Active, membership.Status);
        Assert.Equal(Permission.ForAdminStaff, membership.EffectivePermissions());
    }

    [Fact]
    public void El_mecánico_con_permisos_finos_los_suma_a_los_base()
    {
        var membership = new Membership(TenantId, UserId, TenantRole.Mechanic,
            [Permission.PricesView]);

        var effective = membership.EffectivePermissions();

        Assert.Contains(Permission.PricesView, effective);
        Assert.Contains(Permission.WorkOrdersRead, effective);
        Assert.DoesNotContain(Permission.PaymentsRead, effective);
    }

    [Fact]
    public void Cambiar_el_rol_reemplaza_los_permisos()
    {
        var membership = new Membership(TenantId, UserId, TenantRole.Mechanic,
            [Permission.PricesView]);

        membership.ChangeRole(TenantRole.AdminStaff);

        Assert.Equal(TenantRole.AdminStaff, membership.Role);
        Assert.Empty(membership.Permissions);
        Assert.Equal(Permission.ForAdminStaff, membership.EffectivePermissions());
    }

    [Fact]
    public void El_owner_tiene_todos_los_permisos()
    {
        var membership = new Membership(TenantId, UserId, TenantRole.Owner);

        Assert.True(membership.EffectivePermissions().IsSupersetOf(Permission.ForAdminStaff));
        Assert.True(membership.EffectivePermissions().IsSupersetOf(Permission.ForMechanic));
    }

    [Fact]
    public void Deshabilitar_y_habilitar()
    {
        var membership = new Membership(TenantId, UserId, TenantRole.Mechanic);

        membership.Disable();
        Assert.Equal(MembershipStatus.Disabled, membership.Status);

        membership.Enable();
        Assert.Equal(MembershipStatus.Active, membership.Status);
    }

    [Fact]
    public void Una_membresía_exige_taller_y_usuario()
    {
        Assert.Throws<ArgumentException>(() => new Membership(Guid.Empty, UserId, TenantRole.Owner));
        Assert.Throws<ArgumentException>(() => new Membership(TenantId, Guid.Empty, TenantRole.Owner));
    }
}
