using TallerControl.Domain.Customers;

namespace TallerControl.Domain.Tests.Customers;

/// <summary>
/// T-CUS-04: normalización de teléfonos a E.164 (doc 04 §3.2, PhoneNormalizer).
/// </summary>
public class PhoneNormalizerTests
{
    [Fact(DisplayName = "T-CUS-04: nulo devuelve nulo")]
    public void Nulo_devuelve_nulo()
    {
        Assert.Null(PhoneNormalizer.Normalize(null));
    }

    [Theory(DisplayName = "T-CUS-04: vacío o solo espacios lanza ArgumentException")]
    [InlineData("")]
    [InlineData("   ")]
    public void Vacio_o_espacios_lanza_excepcion(string phone)
    {
        Assert.Throws<ArgumentException>(() => PhoneNormalizer.Normalize(phone));
    }

    [Theory(DisplayName = "T-CUS-04: número ya en E.164 se devuelve sin cambios")]
    [InlineData("+549115551234")]   // Argentina, móvil Buenos Aires, 7 dígitos
    [InlineData("+5491155551234")]  // Argentina, móvil Buenos Aires, 8 dígitos
    [InlineData("+15551234567")]    // Estados Unidos
    public void E164_devuelve_sin_cambios(string phone)
    {
        Assert.Equal(phone, PhoneNormalizer.Normalize(phone));
    }

    // 011 15-555-1234: área BA (011), prefijo móvil (15), número 7 dígitos (5551234)
    // stripped → "011155551234"; rest = "11155551234"; afterArea = "155551234" (9 chars, starts with "15")
    // resultado: +549 + 11 + 5551234 = +549115551234
    [Fact(DisplayName = "T-CUS-04: formato 011 15-NNN-NNNN se normaliza a E.164 argentino móvil")]
    public void Buenos_aires_movil_normaliza_a_e164()
    {
        var result = PhoneNormalizer.Normalize("011 15-555-1234");
        Assert.Equal("+549115551234", result);
    }

    [Fact(DisplayName = "T-CUS-04: número sin prefijo de discado (7 dígitos) lanza ArgumentException")]
    public void Sin_codigo_de_area_lanza_excepcion()
    {
        // 7 dígitos sin '0' ni '+' al inicio: no se puede normalizar
        Assert.Throws<ArgumentException>(() => PhoneNormalizer.Normalize("5551234"));
    }

    [Fact(DisplayName = "T-CUS-04: número de otra área argentina se normaliza")]
    public void Cordoba_movil_normaliza_a_e164()
    {
        // 0351 15-555-1234: área 351 (Córdoba), prefijo móvil (15), número 7 dígitos
        // stripped → "0351155551234"; rest = "351155551234"; area3 = "351", afterArea3 = "155551234" (9 chars, starts with "15")
        // resultado: +549 + 351 + 5551234 = +5493515551234
        var result = PhoneNormalizer.Normalize("0351 15-555-1234");
        Assert.Equal("+5493515551234", result);
    }
}
