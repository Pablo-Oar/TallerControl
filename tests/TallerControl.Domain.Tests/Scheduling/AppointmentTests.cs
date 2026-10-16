using TallerControl.Domain.Scheduling;

namespace TallerControl.Domain.Tests.Scheduling;

/// <summary>
/// Pruebas unitarias de la máquina de estados de Appointment.
/// T-APT-04: bloque total = DurationMinutes + BufferMinutes.
/// </summary>
public sealed class AppointmentTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly byte[] FakeHash = new byte[32];
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static Appointment MakeAppointment(Guid? mechanicId = null) =>
        new(TenantId, CustomerId, VehicleId, mechanicId, Now.AddDays(1), notes: null, FakeHash);

    private static AppointmentService MakeService(Guid? apptId, int durationMinutes, int bufferMinutes)
    {
        var id = apptId ?? Guid.NewGuid();
        return new AppointmentService(id, Guid.NewGuid(), durationMinutes, bufferMinutes);
    }

    // ── T-APT-04: bloque total ────────────────────────────────────────────────

    [Fact(DisplayName = "T-APT-04: servicio 45 min + 15 buffer = 60 min TotalBlockMinutes")]
    public void Servicio_bloque_total_es_duracion_mas_buffer()
    {
        var service = MakeService(Guid.NewGuid(), durationMinutes: 45, bufferMinutes: 15);
        Assert.Equal(60, service.TotalBlockMinutes);
    }

    [Fact(DisplayName = "T-APT-04: agregar servicio actualiza TotalDurationMinutes del turno")]
    public void Agregar_servicio_actualiza_duracion_total()
    {
        var appt = MakeAppointment();
        var service = MakeService(appt.Id, durationMinutes: 45, bufferMinutes: 15);

        appt.AddService(service);

        Assert.Equal(60, appt.TotalDurationMinutes);
    }

    [Fact(DisplayName = "T-APT-04: dos servicios suman sus bloques totales")]
    public void Dos_servicios_suman_bloques()
    {
        var appt = MakeAppointment();
        appt.AddService(MakeService(appt.Id, 45, 15)); // 60
        appt.AddService(MakeService(appt.Id, 30, 10)); // 40

        Assert.Equal(100, appt.TotalDurationMinutes);
    }

    // ── Estado: Confirm ───────────────────────────────────────────────────────

    [Fact(DisplayName = "Turno Pending puede confirmarse")]
    public void Turno_pending_puede_confirmarse()
    {
        var appt = MakeAppointment();
        appt.Confirm();

        Assert.Equal(AppointmentStatus.Confirmed, appt.Status);
        Assert.NotNull(appt.ConfirmationTokenHash); // hash se conserva para detectar segundo uso (T-APT-03)
    }

    [Fact(DisplayName = "T-APT-03: confirmar un turno ya Confirmed lanza excepción")]
    public void Turno_ya_confirmado_no_puede_confirmarse_de_nuevo()
    {
        var appt = MakeAppointment();
        appt.Confirm();

        Assert.Throws<InvalidOperationException>(() => appt.Confirm());
    }

    [Fact(DisplayName = "Turno Cancelled no puede confirmarse")]
    public void Turno_cancelado_no_puede_confirmarse()
    {
        var appt = MakeAppointment();
        appt.Cancel("Motivo de prueba");

        Assert.Throws<InvalidOperationException>(() => appt.Confirm());
    }

    // ── Estado: Complete ──────────────────────────────────────────────────────

    [Fact(DisplayName = "Turno Pending puede completarse")]
    public void Turno_pending_puede_completarse()
    {
        var appt = MakeAppointment();
        appt.Complete();

        Assert.Equal(AppointmentStatus.Completed, appt.Status);
    }

    [Fact(DisplayName = "Turno Confirmed puede completarse")]
    public void Turno_confirmed_puede_completarse()
    {
        var appt = MakeAppointment();
        appt.Confirm();
        appt.Complete();

        Assert.Equal(AppointmentStatus.Completed, appt.Status);
    }

    [Fact(DisplayName = "Turno Cancelled no puede completarse")]
    public void Turno_cancelado_no_puede_completarse()
    {
        var appt = MakeAppointment();
        appt.Cancel("Motivo de prueba");

        Assert.Throws<InvalidOperationException>(() => appt.Complete());
    }

    // ── Estado: Cancel ────────────────────────────────────────────────────────

    [Fact(DisplayName = "Turno Pending puede cancelarse con motivo")]
    public void Turno_pending_puede_cancelarse()
    {
        var appt = MakeAppointment();
        appt.Cancel("No puede venir");

        Assert.Equal(AppointmentStatus.Cancelled, appt.Status);
        Assert.Equal("No puede venir", appt.CancellationReason);
    }

    [Fact(DisplayName = "Cancelar sin motivo lanza excepción")]
    public void Cancelar_sin_motivo_lanza_excepcion()
    {
        var appt = MakeAppointment();

        Assert.Throws<ArgumentException>(() => appt.Cancel(""));
    }

    [Fact(DisplayName = "Turno Completed no puede cancelarse")]
    public void Turno_completado_no_puede_cancelarse()
    {
        var appt = MakeAppointment();
        appt.Complete();

        Assert.Throws<InvalidOperationException>(() => appt.Cancel("Motivo"));
    }

    // ── AssignMechanic ────────────────────────────────────────────────────────

    [Fact(DisplayName = "T-APT-01: AssignMechanic establece MechanicMembershipId")]
    public void Assign_mechanic_establece_id()
    {
        var appt = MakeAppointment();
        var mechId = Guid.NewGuid();
        appt.AssignMechanic(mechId);

        Assert.Equal(mechId, appt.MechanicMembershipId);
    }

    [Fact(DisplayName = "AssignMechanic con Guid.Empty lanza excepción")]
    public void Assign_mechanic_vacio_lanza_excepcion()
    {
        var appt = MakeAppointment();

        Assert.Throws<ArgumentException>(() => appt.AssignMechanic(Guid.Empty));
    }
}
