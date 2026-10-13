using TallerControl.Domain.Vehicles;

namespace TallerControl.Domain.Tests.Vehicles;

/// <summary>
/// T-VEH-02: normalización de patente (doc 04 §3.2, Vehicle.NormalizePlate).
/// T-VEH-06: actualización de odómetro con y sin permiso de corrección (REQ-06, Vehicle.UpdateOdometer).
/// </summary>
public class VehicleTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid CustomerId = Guid.CreateVersion7();

    // ── T-VEH-02: NormalizePlate ─────────────────────────────────────────────

    [Theory(DisplayName = "T-VEH-02: patentes con espacios o guiones se normalizan a mayúsculas")]
    [InlineData("abc 123", "ABC123")]
    [InlineData("AA-123-BB", "AA123BB")]
    [InlineData("ab-cde", "ABCDE")]
    public void Patente_se_normaliza_a_mayusculas_sin_separadores(string input, string expected)
    {
        Assert.Equal(expected, Vehicle.NormalizePlate(input));
    }

    [Fact(DisplayName = "T-VEH-02: patente de 4 caracteres (< 5) lanza ArgumentException")]
    public void Patente_menor_a_5_chars_lanza_excepcion()
    {
        Assert.Throws<ArgumentException>(() => Vehicle.NormalizePlate("AB12"));
    }

    [Fact(DisplayName = "T-VEH-02: patente de 11 caracteres (> 10) lanza ArgumentException")]
    public void Patente_mayor_a_10_chars_lanza_excepcion()
    {
        Assert.Throws<ArgumentException>(() => Vehicle.NormalizePlate("ABCDE12345X"));
    }

    [Theory(DisplayName = "T-VEH-02: patentes vacías o nulas lanzan ArgumentException")]
    [InlineData("")]
    [InlineData("   ")]
    public void Patente_vacia_lanza_excepcion(string plate)
    {
        Assert.Throws<ArgumentException>(() => Vehicle.NormalizePlate(plate));
    }

    // ── T-VEH-06: UpdateOdometer ──────────────────────────────────────────────

    [Fact(DisplayName = "T-VEH-06: subir el km siempre funciona sin permiso especial")]
    public void Subir_km_sin_permiso_funciona()
    {
        var vehicle = new Vehicle(TenantId, CustomerId, "ABC123", odometerKm: 400);
        vehicle.UpdateOdometer(500, allowDecrease: false);
        Assert.Equal(500, vehicle.OdometerKm);
    }

    [Fact(DisplayName = "T-VEH-06: bajar km sin permiso lanza InvalidOperationException")]
    public void Bajar_km_sin_permiso_lanza_excepcion()
    {
        var vehicle = new Vehicle(TenantId, CustomerId, "ABC123", odometerKm: 400);
        Assert.Throws<InvalidOperationException>(
            () => vehicle.UpdateOdometer(300, allowDecrease: false));
    }

    [Fact(DisplayName = "T-VEH-06: bajar km con permiso explícito funciona")]
    public void Bajar_km_con_permiso_funciona()
    {
        var vehicle = new Vehicle(TenantId, CustomerId, "ABC123", odometerKm: 400);
        vehicle.UpdateOdometer(300, allowDecrease: true);
        Assert.Equal(300, vehicle.OdometerKm);
    }

    [Fact(DisplayName = "T-VEH-06: km negativo lanza ArgumentOutOfRangeException")]
    public void Km_negativo_lanza_excepcion()
    {
        var vehicle = new Vehicle(TenantId, CustomerId, "ABC123", odometerKm: 0);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => vehicle.UpdateOdometer(-1, allowDecrease: true));
    }

    [Fact(DisplayName = "T-VEH-06: mantener el mismo km no lanza excepción")]
    public void Mantener_km_igual_no_falla()
    {
        var vehicle = new Vehicle(TenantId, CustomerId, "ABC123", odometerKm: 400);
        vehicle.UpdateOdometer(400, allowDecrease: false); // mismo valor → no se compara como "baja"
        Assert.Equal(400, vehicle.OdometerKm);
    }
}
