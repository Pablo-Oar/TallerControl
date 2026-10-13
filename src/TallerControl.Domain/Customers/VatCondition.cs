namespace TallerControl.Domain.Customers;

/// <summary>Condición frente al IVA (doc 04 §3.2, REQ-05).</summary>
public enum VatCondition
{
    ConsumidorFinal,
    ResponsableInscripto,
    Monotributo,
    Exento,
    NoResponsable,
}
