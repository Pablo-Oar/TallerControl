using TallerControl.Domain.Qr;

namespace TallerControl.Domain.Tests.Qr;

/// <summary>
/// Pruebas unitarias de la máquina de estados de QrCard (T-QR-01).
/// </summary>
public sealed class QrCardTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static QrCard MakeCard(string? code = null) =>
        new(TenantId, code ?? QrCodeGenerator.GenerateCode(), Guid.NewGuid(), Now);

    // --- Assign ---

    [Fact(DisplayName = "T-QR-01: tarjeta Free puede asignarse a un vehículo")]
    public void Tarjeta_free_puede_asignarse()
    {
        var card = MakeCard();
        card.Assign(VehicleId, Now);

        Assert.Equal(QrCardStatus.Assigned, card.Status);
        Assert.Equal(VehicleId, card.VehicleId);
        Assert.Equal(Now, card.AssignedAt);
    }

    [Fact(DisplayName = "T-QR-01: tarjeta Assigned ya asignada no puede reasignarse")]
    public void Tarjeta_assigned_no_puede_reasignarse()
    {
        var card = MakeCard();
        card.Assign(VehicleId, Now);

        Assert.Throws<InvalidOperationException>(() =>
            card.Assign(Guid.NewGuid(), Now));
    }

    [Fact(DisplayName = "T-QR-01: tarjeta Revoked no puede asignarse")]
    public void Tarjeta_revocada_no_puede_asignarse()
    {
        var card = MakeCard();
        card.Revoke("Perdida definitiva", Now);

        Assert.Throws<InvalidOperationException>(() =>
            card.Assign(VehicleId, Now));
    }

    // --- Unassign ---

    [Fact(DisplayName = "T-QR-01: tarjeta Assigned puede desasignarse")]
    public void Tarjeta_assigned_puede_desasignarse()
    {
        var card = MakeCard();
        card.Assign(VehicleId, Now);
        card.Unassign();

        Assert.Equal(QrCardStatus.Free, card.Status);
        Assert.Null(card.VehicleId);
    }

    [Fact(DisplayName = "T-QR-01: tarjeta Lost puede desasignarse")]
    public void Tarjeta_lost_puede_desasignarse()
    {
        var card = MakeCard();
        card.Assign(VehicleId, Now);
        card.MarkLost();
        card.Unassign();

        Assert.Equal(QrCardStatus.Free, card.Status);
    }

    [Fact(DisplayName = "T-QR-01: tarjeta Free no puede desasignarse")]
    public void Tarjeta_free_no_puede_desasignarse()
    {
        var card = MakeCard();

        Assert.Throws<InvalidOperationException>(() => card.Unassign());
    }

    // --- MarkLost / UnmarkLost ---

    [Fact(DisplayName = "T-QR-01: tarjeta Assigned puede marcarse como perdida")]
    public void Tarjeta_assigned_puede_marcarse_perdida()
    {
        var card = MakeCard();
        card.Assign(VehicleId, Now);
        card.MarkLost();

        Assert.Equal(QrCardStatus.Lost, card.Status);
    }

    [Fact(DisplayName = "T-QR-01: tarjeta Lost puede desmarcarse como perdida")]
    public void Tarjeta_lost_puede_desmarcarse()
    {
        var card = MakeCard();
        card.Assign(VehicleId, Now);
        card.MarkLost();
        card.UnmarkLost();

        Assert.Equal(QrCardStatus.Assigned, card.Status);
    }

    [Fact(DisplayName = "T-QR-01: tarjeta Free no puede marcarse como perdida")]
    public void Tarjeta_free_no_puede_marcarse_perdida()
    {
        var card = MakeCard();

        Assert.Throws<InvalidOperationException>(() => card.MarkLost());
    }

    // --- Revoke ---

    [Fact(DisplayName = "T-QR-01: cualquier tarjeta puede revocarse")]
    public void Cualquier_tarjeta_puede_revocarse()
    {
        var free = MakeCard();
        free.Revoke("Motivo", Now);
        Assert.Equal(QrCardStatus.Revoked, free.Status);

        var assigned = MakeCard();
        assigned.Assign(VehicleId, Now);
        assigned.Revoke("Motivo", Now);
        Assert.Equal(QrCardStatus.Revoked, assigned.Status);

        var lost = MakeCard();
        lost.Assign(VehicleId, Now);
        lost.MarkLost();
        lost.Revoke("Motivo", Now);
        Assert.Equal(QrCardStatus.Revoked, lost.Status);
    }

    [Fact(DisplayName = "T-QR-01: tarjeta Revoked no puede volver a revocarse")]
    public void Tarjeta_revocada_no_puede_revocarse_de_nuevo()
    {
        var card = MakeCard();
        card.Revoke("Primera", Now);

        Assert.Throws<InvalidOperationException>(() =>
            card.Revoke("Segunda", Now));
    }

    [Fact(DisplayName = "T-QR-01: revocar sin motivo lanza ArgumentException")]
    public void Revocar_sin_motivo_lanza_excepcion()
    {
        var card = MakeCard();

        Assert.Throws<ArgumentException>(() => card.Revoke(string.Empty, Now));
        Assert.Throws<ArgumentException>(() => card.Revoke("   ", Now));
    }

    // --- QrCodeGenerator ---

    [Fact(DisplayName = "T-QR-01: el código generado tiene 10 caracteres y solo usa el alfabeto Base32 Crockford")]
    public void Codigo_generado_es_valido()
    {
        const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        for (var i = 0; i < 100; i++)
        {
            var code = QrCodeGenerator.GenerateCode();
            Assert.Equal(QrCodeGenerator.CodeLength, code.Length);
            Assert.All(code, c => Assert.Contains(c, alphabet));
        }
    }

    [Fact(DisplayName = "T-QR-01: dos códigos generados consecutivamente son diferentes (entropía ~50 bits)")]
    public void Codigos_consecutivos_son_diferentes()
    {
        var codes = Enumerable.Range(0, 1000).Select(_ => QrCodeGenerator.GenerateCode()).ToHashSet();
        // Con ~50 bits de entropía, la probabilidad de colisión en 1000 intentos es despreciable
        Assert.True(codes.Count > 990, $"Demasiadas colisiones: {1000 - codes.Count} en 1000 códigos");
    }
}
