using PeliculasAPI.Tests.Integration.Infraestructura;

namespace PeliculasAPI.Tests.Integration;

public class CinesEndpointsTests(ApiFactory fabrica) : IClassFixture<ApiFactory>
{
    private const string Ruta = "/api/v1/cines";

    [Fact]
    public async Task CrearYObtener_ConservaLatitudYLongitud()
    {
        var admin = fabrica.CrearClienteAdmin();

        var id = await admin.CrearCineAsync(latitud: 21.1236, longitud: -101.6869);
        var cine = await admin.GetFromJsonAsync<JsonElement>($"{Ruta}/{id}");

        // Ida y vuelta por SQL Server (tipo geography): comprueba que latitud y longitud no se invierten.
        Assert.Equal(21.1236, cine.GetProperty("latitud").GetDouble(), precision: 5);
        Assert.Equal(-101.6869, cine.GetProperty("longitud").GetDouble(), precision: 5);
    }

    [Theory]
    [InlineData(90.0, 180.0, HttpStatusCode.Created)]       // esquina máxima válida
    [InlineData(-90.0, -180.0, HttpStatusCode.Created)]     // esquina mínima válida
    [InlineData(90.1, 0.0, HttpStatusCode.BadRequest)]      // latitud fuera de rango
    [InlineData(-90.1, 0.0, HttpStatusCode.BadRequest)]
    [InlineData(0.0, 180.1, HttpStatusCode.BadRequest)]     // longitud fuera de rango
    [InlineData(0.0, -180.1, HttpStatusCode.BadRequest)]
    public async Task Crear_ConCoordenadasEnLosLimites(double latitud, double longitud, HttpStatusCode esperado)
    {
        var respuesta = await fabrica.CrearClienteAdmin()
            .PostAsJsonAsync(Ruta, new { nombre = $"Cine {Guid.NewGuid():N}", latitud, longitud });

        Assert.Equal(esperado, respuesta.StatusCode);
    }

    [Theory]
    [InlineData(75, HttpStatusCode.Created)]      // límite exacto del [StringLength(75)]
    [InlineData(76, HttpStatusCode.BadRequest)]
    public async Task Crear_ConLongitudDeNombreEnElLimite(int longitud, HttpStatusCode esperado)
    {
        var respuesta = await fabrica.CrearClienteAdmin()
            .PostAsJsonAsync(Ruta, new { nombre = new string('C', longitud), latitud = 10.0, longitud = 10.0 });

        Assert.Equal(esperado, respuesta.StatusCode);
    }

    [Fact]
    public async Task Crear_SinToken_Responde401()
    {
        var respuesta = await fabrica.CrearCliente()
            .PostAsJsonAsync(Ruta, new { nombre = "Cine", latitud = 1.0, longitud = 1.0 });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Actualizar_Existente_Responde204YPersisteLaNuevaUbicacion()
    {
        var admin = fabrica.CrearClienteAdmin();
        var id = await admin.CrearCineAsync();

        var respuesta = await admin.PutAsJsonAsync($"{Ruta}/{id}",
            new { nombre = "Cine Renovado", latitud = 19.4326, longitud = -99.1332 });

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        var cine = await admin.GetFromJsonAsync<JsonElement>($"{Ruta}/{id}");
        Assert.Equal("Cine Renovado", cine.GetProperty("nombre").GetString());
        Assert.Equal(19.4326, cine.GetProperty("latitud").GetDouble(), precision: 5);
        Assert.Equal(-99.1332, cine.GetProperty("longitud").GetDouble(), precision: 5);
    }

    [Fact]
    public async Task Actualizar_Inexistente_Responde404()
    {
        var respuesta = await fabrica.CrearClienteAdmin()
            .PutAsJsonAsync($"{Ruta}/999999", new { nombre = "Nada", latitud = 1.0, longitud = 1.0 });

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Eliminar_Existente_Responde204()
    {
        var admin = fabrica.CrearClienteAdmin();
        var id = await admin.CrearCineAsync();

        var respuesta = await admin.DeleteAsync($"{Ruta}/{id}");

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }
}
