namespace TallerControl.Domain.Invoicing;

/// <summary>
/// Tipos de comprobante ARCA (WSFEv1). El tipo emitido depende de la condición IVA del receptor.
/// </summary>
public enum InvoiceType
{
    A,
    B,
    C,
    M,
}
