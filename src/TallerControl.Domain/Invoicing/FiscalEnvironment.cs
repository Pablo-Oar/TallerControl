namespace TallerControl.Domain.Invoicing;

/// <summary>
/// Ambiente de ARCA al que apunta un certificado (D-33). ARCA emite certificados distintos para
/// homologación (pruebas, sin validez fiscal) y producción.
/// </summary>
public enum FiscalEnvironment
{
    Production,
    Homologation,
}
