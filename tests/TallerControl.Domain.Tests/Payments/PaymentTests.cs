using TallerControl.Domain.Payments;

namespace TallerControl.Domain.Tests.Payments;

/// <summary>
/// Pruebas unitarias del dominio Payment (T-PAY-01 parcial, T-PAY-05, T-PAY-06).
/// </summary>
public sealed class PaymentTests
{
    [Fact(DisplayName = "T-PAY: monto negativo o cero lanza ArgumentOutOfRangeException")]
    public void Monto_invalido_lanza_excepcion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MakePayment(amount: 0m));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MakePayment(amount: -1m));
    }

    [Fact(DisplayName = "T-PAY: pago válido se crea en estado Valid")]
    public void Pago_nuevo_es_valido()
    {
        var payment = MakePayment();

        Assert.Equal(PaymentStatus.Valid, payment.Status);
        Assert.Null(payment.VoidReason);
        Assert.Null(payment.VoidedAt);
    }

    [Fact(DisplayName = "T-PAY-06: Anular un pago válido lo marca como Voided")]
    public void Anular_pago_valido_funciona()
    {
        var payment = MakePayment();
        var at = DateTimeOffset.UtcNow;

        payment.Anular("Error de caja", by: null, at);

        Assert.Equal(PaymentStatus.Voided, payment.Status);
        Assert.Equal("Error de caja", payment.VoidReason);
        Assert.Equal(at, payment.VoidedAt);
    }

    [Fact(DisplayName = "T-PAY-06: Anular un pago ya anulado lanza InvalidOperationException")]
    public void Anular_pago_ya_anulado_lanza_excepcion()
    {
        var payment = MakePayment();
        payment.Anular("Primera vez", by: null, DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            payment.Anular("Segunda vez", by: null, DateTimeOffset.UtcNow));
    }

    [Fact(DisplayName = "T-PAY-06: Anular sin motivo lanza ArgumentException")]
    public void Anular_sin_motivo_lanza_excepcion()
    {
        var payment = MakePayment();

        Assert.Throws<ArgumentException>(() =>
            payment.Anular(string.Empty, by: null, DateTimeOffset.UtcNow));

        Assert.Throws<ArgumentException>(() =>
            payment.Anular("   ", by: null, DateTimeOffset.UtcNow));
    }

    private static Payment MakePayment(decimal amount = 500m) =>
        new(
            tenantId: Guid.NewGuid(),
            workOrderId: Guid.NewGuid(),
            number: 1,
            method: PaymentMethod.Cash,
            amount: amount,
            paidAt: DateTimeOffset.UtcNow,
            reference: null,
            publicTokenHash: new byte[32],
            idempotencyKey: null,
            createdBy: null,
            createdAt: DateTimeOffset.UtcNow);
}
