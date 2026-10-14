namespace TallerControl.Domain.Payments;

/// <summary>Medios de cobro aceptados en P0 (REQ-15).</summary>
public enum PaymentMethod
{
    Cash,
    Transfer,
    Debit,
    Credit,
}
