using TallerControl.Domain.Users;

namespace TallerControl.Domain.Tests.Users;

public class UserTests
{
    [Fact]
    public void Un_usuario_nuevo_queda_pendiente_y_sin_contraseña()
    {
        var user = new User("pablo@taller.com", "Pablo");

        Assert.Equal(UserStatus.Pending, user.Status);
        Assert.Null(user.PasswordHash);
        Assert.Equal("pablo@taller.com", user.Email);
        Assert.Equal("Pablo", user.DisplayName);
        Assert.False(string.IsNullOrWhiteSpace(user.SecurityStamp));
    }

    [Fact]
    public void Al_activarse_queda_activo_con_contraseña()
    {
        var user = new User("pablo@taller.com", "Pablo");
        user.Activate("hash_seguro");

        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal("hash_seguro", user.PasswordHash);
    }

    [Fact]
    public void Activar_sin_hash_falla()
    {
        var user = new User("pablo@taller.com", "Pablo");
        Assert.Throws<ArgumentException>(() => user.Activate(""));
        Assert.Throws<ArgumentException>(() => user.Activate("  "));
    }

    [Fact]
    public void El_bloqueo_se_activa_al_quinto_fallo()
    {
        var user = new User("pablo@taller.com", "Pablo");
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < User.MaxFailedAttempts - 1; i++)
        {
            user.RecordFailedLogin(now);
            Assert.False(user.IsLockedOut(now));
        }

        user.RecordFailedLogin(now);
        Assert.True(user.IsLockedOut(now));
        Assert.False(user.IsLockedOut(now + User.LockoutDuration + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Un_login_exitoso_limpia_los_fallos()
    {
        var user = new User("pablo@taller.com", "Pablo");
        var now = DateTimeOffset.UtcNow;

        user.RecordFailedLogin(now);
        user.RecordFailedLogin(now);
        user.ResetAccessFailed();

        Assert.Equal(0, user.AccessFailedCount);
        Assert.Null(user.LockoutEnd);
    }

    [Fact]
    public void Rotar_el_security_stamp_cambia_el_valor()
    {
        var user = new User("pablo@taller.com", "Pablo");
        var original = user.SecurityStamp;

        user.RotateSecurityStamp();

        Assert.NotEqual(original, user.SecurityStamp);
    }

    [Fact]
    public void El_email_se_normaliza_a_minúsculas_y_se_recorta()
    {
        var user = new User("  Pablo@Taller.COM  ", "Pablo");
        Assert.Equal("pablo@taller.com", user.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-arroba")]
    public void Un_email_invalido_falla(string email)
    {
        Assert.Throws<ArgumentException>(() => new User(email, "Pablo"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Un_nombre_vacio_falla(string name)
    {
        Assert.Throws<ArgumentException>(() => new User("pablo@taller.com", name));
    }

    [Fact]
    public void Un_nombre_de_151_caracteres_falla()
    {
        Assert.Throws<ArgumentException>(() => new User("pablo@taller.com", new string('a', 151)));
        _ = new User("pablo@taller.com", new string('a', 150));
    }

    [Fact]
    public void Deshabilitar_cambia_el_estado()
    {
        var user = new User("pablo@taller.com", "Pablo");
        user.Activate("hash");
        user.Disable();

        Assert.Equal(UserStatus.Disabled, user.Status);
    }
}
