namespace TallerControl.Domain.Customers;

/// <summary>Tipo de documento identificatorio del cliente (doc 04 §3.2, REQ-05).</summary>
public enum DocType
{
    Dni,
    Cuit,
    Cuil,
    Pasaporte,
    Otro,
}
