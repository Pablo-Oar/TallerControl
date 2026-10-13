using TallerControl.Domain.Users;

namespace TallerControl.Domain.Tests.Users;

public class InvitationTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();

    [Fact]
    public void Crear_genera_un_token_y_guarda_solo_el_hash()
    {
        var now = DateTimeOffset.UtcNow;
        var (invitation, token) = Invitation.Create(
            TenantId, "nuevo@taller.com", TenantRole.Mechanic, null, now);

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.NotEmpty(invitation.TokenHash);
        Assert.Equal(now + Invitation.Validity, invitation.ExpiresAt);
        Assert.False(invitation.IsAccepted);
        Assert.Equal("nuevo@taller.com", invitation.Email);
        Assert.Equal(TenantRole.Mechanic, invitation.Role);
    }

    [Fact]
    public void El_hash_del_token_coincide_con_el_que_se_genera()
    {
        var (invitation, token) = Invitation.Create(
            TenantId, "nuevo@taller.com", TenantRole.Mechanic, null, DateTimeOffset.UtcNow);

        Assert.Equal(invitation.TokenHash, Invitation.HashToken(token));
    }

    [Fact]
    public void Aceptar_marca_la_invitación()
    {
        var now = DateTimeOffset.UtcNow;
        var (invitation, _) = Invitation.Create(
            TenantId, "nuevo@taller.com", TenantRole.Mechanic, null, now);

        invitation.Accept(now + TimeSpan.FromHours(1));

        Assert.True(invitation.IsAccepted);
    }

    [Fact]
    public void No_se_puede_aceptar_dos_veces()
    {
        var now = DateTimeOffset.UtcNow;
        var (invitation, _) = Invitation.Create(
            TenantId, "nuevo@taller.com", TenantRole.Mechanic, null, now);
        invitation.Accept(now);

        Assert.Throws<InvalidOperationException>(() => invitation.Accept(now));
    }

    [Fact]
    public void No_se_puede_aceptar_una_invitación_vencida()
    {
        var now = DateTimeOffset.UtcNow;
        var (invitation, _) = Invitation.Create(
            TenantId, "nuevo@taller.com", TenantRole.Mechanic, null, now);

        Assert.Throws<InvalidOperationException>(() =>
            invitation.Accept(now + Invitation.Validity + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void La_invitación_guarda_permisos_finos()
    {
        var (invitation, _) = Invitation.Create(
            TenantId, "nuevo@taller.com", TenantRole.Mechanic,
            [Permission.PricesView, Permission.ContactView], DateTimeOffset.UtcNow);

        Assert.Equal(2, invitation.Permissions.Count);
        Assert.Contains(Permission.PricesView, invitation.Permissions);
    }
}
