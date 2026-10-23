using TallerControl.Domain.Common;
using TallerControl.Domain.Tenancy;

namespace TallerControl.Domain.Tests;

public sealed class CuitTests
{
    [Theory(DisplayName = "T-CUIT-01: acepta CUIT con verificador correcto, con o sin guiones")]
    [InlineData("20-12345678-6")]
    [InlineData("20123456786")]
    [InlineData(" 20 12345678 6")]
    public void Valido(string value) => Assert.True(Cuit.IsValid(value));

    [Theory(DisplayName = "T-CUIT-02: rechaza verificador incorrecto, largo erróneo y letras")]
    [InlineData("20-12345678-5")]
    [InlineData("2012345678")]
    [InlineData("20-1234567x-6")]
    [InlineData("")]
    [InlineData(null)]
    public void Invalido(string? value) => Assert.False(Cuit.IsValid(value));

    [Fact(DisplayName = "T-CUIT-03: Format deja el CUIT como XX-XXXXXXXX-X")]
    public void Formato() => Assert.Equal("20-12345678-6", Cuit.Format("20123456786"));
}
