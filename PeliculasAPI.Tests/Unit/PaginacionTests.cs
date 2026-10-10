using PeliculasAPI.DTOs;
using PeliculasAPI.Utilidades;

namespace PeliculasAPI.Tests.Unit;

public class PaginacionTests
{
    // Fixture: diez elementos, 1..10, consultables como IQueryable.
    private static IQueryable<int> Numeros() => Enumerable.Range(1, 10).AsQueryable();

    [Fact]
    public void ValoresPorDefecto_SonPagina1YTresRegistros()
    {
        var paginacion = new PaginacionDTO();

        Assert.Equal(1, paginacion.Pagina);
        Assert.Equal(3, paginacion.RecordsPorPagina);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(50, 50)]      // límite exacto: se respeta
    [InlineData(51, 50)]      // un registro por encima: se recorta
    [InlineData(10000, 50)]
    public void RecordsPorPagina_NuncaSuperaElMaximoDe50(int solicitado, int esperado)
    {
        var paginacion = new PaginacionDTO { RecordsPorPagina = solicitado };

        Assert.Equal(esperado, paginacion.RecordsPorPagina);
    }

    [Theory]
    [InlineData(1, 3, new[] { 1, 2, 3 })]                              // primera página
    [InlineData(2, 3, new[] { 4, 5, 6 })]                              // página intermedia
    [InlineData(4, 3, new[] { 10 })]                                   // última página incompleta
    [InlineData(5, 3, new int[0])]                                     // página fuera de rango
    [InlineData(1, 50, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 })]       // página mayor que los datos
    public void Paginar_DevuelveLaPaginaSolicitada(int pagina, int registros, int[] esperado)
    {
        var paginacion = new PaginacionDTO { Pagina = pagina, RecordsPorPagina = registros };

        var resultado = Numeros().Paginar(paginacion).ToArray();

        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public void Paginar_ConColeccionVacia_DevuelveVacio()
    {
        var resultado = Enumerable.Empty<int>().AsQueryable().Paginar(new PaginacionDTO()).ToArray();

        Assert.Empty(resultado);
    }
}
