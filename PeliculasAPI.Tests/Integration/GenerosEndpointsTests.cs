using PeliculasAPI.Tests.Integration.Infraestructura;

namespace PeliculasAPI.Tests.Integration;

public class GenerosEndpointsTests(ApiFactory fabrica) : IClassFixture<ApiFactory>
{
    private const string Ruta = "/api/v1/generos";

    // ---------- Autenticación y autorización ----------

    [Fact]
    public async Task ObtenerTodos_SinToken_Responde200()
    {
        var respuesta = await fabrica.CrearCliente().GetAsync($"{Ruta}/todos");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task ObtenerListado_SinToken_Responde401()
    {
        var respuesta = await fabrica.CrearCliente().GetAsync(Ruta);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task ObtenerListado_ConTokenSinClaimAdmin_Responde403()
    {
        var cliente = fabrica.CrearCliente(ApiFactory.CrearToken("alguien@pruebas.test", esAdmin: false));

        var respuesta = await cliente.GetAsync(Ruta);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Crear_SinToken_Responde401()
    {
        var respuesta = await fabrica.CrearCliente().PostAsJsonAsync(Ruta, new { nombre = "Comedia" });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    // ---------- Casos normales ----------

    [Fact]
    public async Task Crear_ComoAdmin_Responde201ConLocationYCuerpo()
    {
        var admin = fabrica.CrearClienteAdmin();
        var nombre = DatosDePrueba.NombreGenero();

        var respuesta = await admin.PostAsJsonAsync(Ruta, new { nombre });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = await respuesta.LeerJsonAsync();
        var id = cuerpo.GetProperty("id").GetInt32();
        Assert.Equal(nombre, cuerpo.GetProperty("nombre").GetString());
        Assert.EndsWith($"{Ruta}/{id}", respuesta.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task Crear_NuevoGenero_EsVisibleEnTodos_TrasInvalidarCache()
    {
        var anonimo = fabrica.CrearCliente();
        var admin = fabrica.CrearClienteAdmin();
        var nombre = DatosDePrueba.NombreGenero();

        await anonimo.GetAsync($"{Ruta}/todos");              // llena la caché de salida
        await admin.CrearGeneroAsync(nombre);                  // la escritura debe invalidarla
        var despues = await anonimo.GetFromJsonAsync<JsonElement>($"{Ruta}/todos");

        Assert.Contains(despues.EnumerateArray(), g => g.GetProperty("nombre").GetString() == nombre);
    }

    [Fact]
    public async Task ObtenerListado_Paginado_RespetaTamanioYEncabezadoTotal()
    {
        var admin = fabrica.CrearClienteAdmin();
        for (var i = 0; i < 3; i++)
            await admin.CrearGeneroAsync();

        var respuesta = await admin.GetAsync($"{Ruta}?pagina=1&recordsPorPagina=2");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var lista = await respuesta.LeerJsonAsync();
        Assert.Equal(2, lista.GetArrayLength());
        var total = int.Parse(respuesta.Headers.GetValues("cantidad-total-registros").Single());
        Assert.True(total >= 3, $"El total debía ser al menos 3 y fue {total}");
    }

    [Fact]
    public async Task Actualizar_Existente_Responde204YPersisteElCambio()
    {
        var admin = fabrica.CrearClienteAdmin();
        var id = await admin.CrearGeneroAsync();
        var nuevoNombre = DatosDePrueba.NombreGenero();

        var respuesta = await admin.PutAsJsonAsync($"{Ruta}/{id}", new { nombre = nuevoNombre });

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        var genero = await admin.GetFromJsonAsync<JsonElement>($"{Ruta}/{id}");
        Assert.Equal(nuevoNombre, genero.GetProperty("nombre").GetString());
    }

    [Fact]
    public async Task Eliminar_Existente_Responde204YLuegoDa404()
    {
        var admin = fabrica.CrearClienteAdmin();
        var id = await admin.CrearGeneroAsync();

        var borrado = await admin.DeleteAsync($"{Ruta}/{id}");
        var consulta = await admin.GetAsync($"{Ruta}/{id}");

        Assert.Equal(HttpStatusCode.NoContent, borrado.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, consulta.StatusCode);
    }

    // ---------- Casos límite ----------

    [Theory]
    [InlineData(50, HttpStatusCode.Created)]      // límite exacto del [StringLength(50)]
    [InlineData(51, HttpStatusCode.BadRequest)]   // un carácter de más
    public async Task Crear_ConLongitudDeNombreEnElLimite(int longitud, HttpStatusCode esperado)
    {
        var nombre = "A" + new string('a', longitud - 1);

        var respuesta = await fabrica.CrearClienteAdmin().PostAsJsonAsync(Ruta, new { nombre });

        Assert.Equal(esperado, respuesta.StatusCode);
    }

    // ---------- Escenarios de falla ----------

    [Theory]
    [InlineData("comedia")]   // primera letra en minúscula
    [InlineData("")]          // vacío
    [InlineData("   ")]       // solo espacios
    public async Task Crear_ConNombreInvalido_Responde400ConErrores(string nombre)
    {
        var respuesta = await fabrica.CrearClienteAdmin().PostAsJsonAsync(Ruta, new { nombre });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.True(cuerpo.TryGetProperty("errors", out _), "La respuesta debe detallar los errores de validación");
    }

    [Fact]
    public async Task Actualizar_Inexistente_Responde404()
    {
        var respuesta = await fabrica.CrearClienteAdmin()
            .PutAsJsonAsync($"{Ruta}/999999", new { nombre = "Inexistente" });

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Eliminar_Inexistente_Responde404()
    {
        var respuesta = await fabrica.CrearClienteAdmin().DeleteAsync($"{Ruta}/999999");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task ObtenerPorId_Inexistente_Responde404()
    {
        var respuesta = await fabrica.CrearClienteAdmin().GetAsync($"{Ruta}/999999");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
