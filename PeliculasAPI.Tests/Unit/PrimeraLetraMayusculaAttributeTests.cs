using System.ComponentModel.DataAnnotations;
using PeliculasAPI.Validaciones;

namespace PeliculasAPI.Tests.Unit;

public class PrimeraLetraMayusculaAttributeTests
{
    // ValidationResult.Success es null, por eso "válido" == resultado nulo.
    private static ValidationResult? Validar(object? valor) =>
        new PrimeraLetraMayusculaAttribute().GetValidationResult(valor, new ValidationContext(new object()));

    [Theory]
    [InlineData("Comedia")]
    [InlineData("Ñandú")]
    [InlineData("Árbol")]
    [InlineData("A")]
    public void ConPrimeraLetraMayuscula_EsValido(string valor)
    {
        Assert.Null(Validar(valor));
    }

    [Theory]
    [InlineData("comedia")]
    [InlineData("ñandú")]
    [InlineData("árbol")]
    [InlineData("a")]
    public void ConPrimeraLetraMinuscula_FallaConMensaje(string valor)
    {
        var resultado = Validar(valor);

        Assert.NotNull(resultado);
        Assert.Contains("mayúscula", resultado!.ErrorMessage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConValorNuloOVacio_EsValido_PorqueRequiredLoCubre(string? valor)
    {
        Assert.Null(Validar(valor));
    }

    [Fact]
    public void ConDigitoInicial_EsValido()
    {
        // Un dígito no tiene variante en minúscula, así que no se rechaza.
        Assert.Null(Validar("3 Idiotas"));
    }
}
