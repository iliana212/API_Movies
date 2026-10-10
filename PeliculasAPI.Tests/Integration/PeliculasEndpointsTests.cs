using PeliculasAPI.Tests.Integration.Infraestructura;

namespace PeliculasAPI.Tests.Integration;

public class PeliculasEndpointsTests(ApiFactory fabrica) : IClassFixture<ApiFactory>
{
    private const string Ruta = "/api/v1/peliculas";

    private static List<string?> Titulos(JsonElement lista) =>
        lista.EnumerateArray().Select(p => p.GetProperty("titulo").GetString()).ToList();

    // ---------- Creación ----------

    [Fact]
    public async Task Crear_ComoAdmin_Responde201ConLocation()
    {
        var admin = fabrica.CrearClienteAdmin();
        using var formulario = DatosDePrueba.FormularioPelicula(DatosDePrueba.Titulo());

        var respuesta = await admin.PostAsync(Ruta, formulario);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var id = await respuesta.LeerIdAsync();
        Assert.EndsWith($"{Ruta}/{id}", respuesta.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task Crear_SinToken_Responde401()
    {
        using var formulario = DatosDePrueba.FormularioPelicula(DatosDePrueba.Titulo());

        var respuesta = await fabrica.CrearCliente().PostAsync(Ruta, formulario);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Crear_ComoUsuarioNoAdmin_Responde403()
    {
        using var formulario = DatosDePrueba.FormularioPelicula(DatosDePrueba.Titulo());
        var usuario = fabrica.CrearCliente(ApiFactory.CrearToken("usuario@pruebas.test"));

        var respuesta = await usuario.PostAsync(Ruta, formulario);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Crear_SinTitulo_Responde400()
    {
        using var formulario = DatosDePrueba.FormularioPelicula(titulo: null);

        var respuesta = await fabrica.CrearClienteAdmin().PostAsync(Ruta, formulario);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Theory]
    [InlineData(300, HttpStatusCode.Created)]      // límite exacto del [StringLength(300)]
    [InlineData(301, HttpStatusCode.BadRequest)]
    public async Task Crear_ConLongitudDeTituloEnElLimite(int longitud, HttpStatusCode esperado)
    {
        using var formulario = DatosDePrueba.FormularioPelicula("T" + new string('x', longitud - 1));

        var respuesta = await fabrica.CrearClienteAdmin().PostAsync(Ruta, formulario);

        Assert.Equal(esperado, respuesta.StatusCode);
    }

    [Fact]
    public async Task Crear_ConGeneroInexistente_Responde400ReferenciaInvalida()
    {
        using var formulario = DatosDePrueba.FormularioPelicula(DatosDePrueba.Titulo(), generos: new[] { 999999 });

        var respuesta = await fabrica.CrearClienteAdmin().PostAsync(Ruta, formulario);

        // Violación de llave foránea: la traduce el manejador global de excepciones.
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await respuesta.LeerJsonAsync();
        Assert.Equal("Referencia inválida", problema.GetProperty("title").GetString());
        Assert.True(problema.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task Crear_ConPoster_GuardaLaUrlDelAlmacenador()
    {
        var admin = fabrica.CrearClienteAdmin();
        var id = await admin.CrearPeliculaAsync(conPoster: true);

        var detalle = await fabrica.CrearCliente().GetFromJsonAsync<JsonElement>($"{Ruta}/{id}");

        Assert.StartsWith("https://almacen.pruebas/peliculas/", detalle.GetProperty("poster").GetString());
    }

    // ---------- Detalle ----------

    [Fact]
    public async Task ObtenerDetalle_IncluyeGenerosCinesYActores()
    {
        var admin = fabrica.CrearClienteAdmin();
        var generoId = await admin.CrearGeneroAsync();
        var cineId = await admin.CrearCineAsync(latitud: 19.4326, longitud: -99.1332);
        var actorId = await admin.CrearActorAsync();
        var peliculaId = await admin.CrearPeliculaAsync(
            generos: new[] { generoId },
            cines: new[] { cineId },
            actores: new[] { (actorId, "Protagonista") });

        var detalle = await fabrica.CrearCliente().GetFromJsonAsync<JsonElement>($"{Ruta}/{peliculaId}");

        Assert.Equal(generoId, detalle.GetProperty("generos")[0].GetProperty("id").GetInt32());
        var cine = detalle.GetProperty("cines")[0];
        Assert.Equal(cineId, cine.GetProperty("id").GetInt32());
        Assert.Equal(19.4326, cine.GetProperty("latitud").GetDouble(), precision: 5);
        Assert.Equal(-99.1332, cine.GetProperty("longitud").GetDouble(), precision: 5);
        var actor = detalle.GetProperty("actores")[0];
        Assert.Equal(actorId, actor.GetProperty("id").GetInt32());
        Assert.Equal("Protagonista", actor.GetProperty("personaje").GetString());
        Assert.Equal(0, detalle.GetProperty("promedioVoto").GetDouble());
        Assert.Equal(0, detalle.GetProperty("votoUsuario").GetInt32());
    }

    [Fact]
    public async Task ObtenerDetalle_Inexistente_Responde404()
    {
        var respuesta = await fabrica.CrearCliente().GetAsync($"{Ruta}/999999");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    // ---------- Landing ----------

    [Fact]
    public async Task Landing_PeliculaFutura_AparecenEnProximosEstrenosYLaPasadaNo()
    {
        // Nota: "próximos estrenos" devuelve las 6 más cercanas. Esta prueba usa "mañana" (la fecha futura
        // más temprana posible) y solo otra prueba de la suite crea películas futuras, así que siempre entra.
        var admin = fabrica.CrearClienteAdmin();
        var tituloFuturo = DatosDePrueba.Titulo("Futura");
        var tituloPasado = DatosDePrueba.Titulo("Pasada");
        await admin.CrearPeliculaAsync(tituloFuturo, fecha: DateTime.Today.AddDays(1));
        await admin.CrearPeliculaAsync(tituloPasado, fecha: new DateTime(2020, 1, 1));

        var landing = await fabrica.CrearCliente().GetFromJsonAsync<JsonElement>($"{Ruta}/landing");

        var proximos = Titulos(landing.GetProperty("proximosEstrenos"));
        Assert.Contains(tituloFuturo, proximos);
        Assert.DoesNotContain(tituloPasado, proximos);
    }

    // ---------- Filtrado ----------

    [Fact]
    public async Task Filtrar_PorTitulo_DevuelveSoloCoincidencias()
    {
        var admin = fabrica.CrearClienteAdmin();
        var marca = Guid.NewGuid().ToString("N");
        await admin.CrearPeliculaAsync($"Alfa {marca}");
        await admin.CrearPeliculaAsync($"Beta {marca}");
        await admin.CrearPeliculaAsync(DatosDePrueba.Titulo("Sin marca"));

        var resultado = await fabrica.CrearCliente().GetFromJsonAsync<JsonElement>($"{Ruta}/filtrar?titulo={marca}");

        Assert.Equal(2, resultado.GetArrayLength());
        Assert.All(Titulos(resultado), titulo => Assert.Contains(marca, titulo));
    }

    [Fact]
    public async Task Filtrar_PorGenero_DevuelveSoloLasDelGenero()
    {
        var admin = fabrica.CrearClienteAdmin();
        var generoId = await admin.CrearGeneroAsync();
        var conGenero = DatosDePrueba.Titulo("ConGenero");
        await admin.CrearPeliculaAsync(conGenero, generos: new[] { generoId });
        await admin.CrearPeliculaAsync(DatosDePrueba.Titulo("SinGenero"));

        var resultado = await fabrica.CrearCliente().GetFromJsonAsync<JsonElement>($"{Ruta}/filtrar?generoId={generoId}");

        Assert.Equal(new List<string?> { conGenero }, Titulos(resultado));
    }

    [Fact]
    public async Task Filtrar_ProximosEstrenos_ExcluyeFechasPasadas()
    {
        var admin = fabrica.CrearClienteAdmin();
        var marca = Guid.NewGuid().ToString("N");
        var futura = $"Futura {marca}";
        await admin.CrearPeliculaAsync(futura, fecha: DateTime.Today.AddDays(1));
        await admin.CrearPeliculaAsync($"Pasada {marca}", fecha: new DateTime(2020, 1, 1));

        var resultado = await fabrica.CrearCliente()
            .GetFromJsonAsync<JsonElement>($"{Ruta}/filtrar?titulo={marca}&proximosEstrenos=true");

        Assert.Equal(new List<string?> { futura }, Titulos(resultado));
    }

    [Fact]
    public async Task Filtrar_ConPaginacion_RespetaTamanioYTotal()
    {
        var admin = fabrica.CrearClienteAdmin();
        var marca = Guid.NewGuid().ToString("N");
        for (var i = 1; i <= 3; i++)
            await admin.CrearPeliculaAsync($"Serie{i} {marca}");
        var cliente = fabrica.CrearCliente();

        var pagina1 = await cliente.GetAsync($"{Ruta}/filtrar?titulo={marca}&pagina=1&recordsPorPagina=2");
        var pagina2 = await cliente.GetAsync($"{Ruta}/filtrar?titulo={marca}&pagina=2&recordsPorPagina=2");

        Assert.Equal(2, (await pagina1.LeerJsonAsync()).GetArrayLength());
        Assert.Equal(1, (await pagina2.LeerJsonAsync()).GetArrayLength());
        Assert.Equal("3", pagina1.Headers.GetValues("cantidad-total-registros").Single());
    }

    [Fact]
    public async Task Filtrar_SinCoincidencias_DevuelveListaVacia()
    {
        var resultado = await fabrica.CrearCliente()
            .GetFromJsonAsync<JsonElement>($"{Ruta}/filtrar?titulo=NoExiste{Guid.NewGuid():N}");

        Assert.Equal(0, resultado.GetArrayLength());
    }

    // ---------- Actualización y borrado ----------

    [Fact]
    public async Task Actualizar_CambiaTituloYGeneros()
    {
        var admin = fabrica.CrearClienteAdmin();
        var generoA = await admin.CrearGeneroAsync();
        var generoB = await admin.CrearGeneroAsync();
        var id = await admin.CrearPeliculaAsync(generos: new[] { generoA });
        var nuevoTitulo = DatosDePrueba.Titulo("Editada");

        using var formulario = DatosDePrueba.FormularioPelicula(nuevoTitulo, generos: new[] { generoB });
        var respuesta = await admin.PutAsync($"{Ruta}/{id}", formulario);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        var detalle = await fabrica.CrearCliente().GetFromJsonAsync<JsonElement>($"{Ruta}/{id}");
        Assert.Equal(nuevoTitulo, detalle.GetProperty("titulo").GetString());
        var genero = Assert.Single(detalle.GetProperty("generos").EnumerateArray());
        Assert.Equal(generoB, genero.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Actualizar_Inexistente_Responde404()
    {
        using var formulario = DatosDePrueba.FormularioPelicula(DatosDePrueba.Titulo());

        var respuesta = await fabrica.CrearClienteAdmin().PutAsync($"{Ruta}/999999", formulario);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Eliminar_Existente_Responde204YLuegoDa404()
    {
        var admin = fabrica.CrearClienteAdmin();
        var id = await admin.CrearPeliculaAsync();

        var borrado = await admin.DeleteAsync($"{Ruta}/{id}");
        var consulta = await fabrica.CrearCliente().GetAsync($"{Ruta}/{id}");

        Assert.Equal(HttpStatusCode.NoContent, borrado.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, consulta.StatusCode);
    }

    [Fact]
    public async Task Eliminar_Inexistente_Responde404()
    {
        var respuesta = await fabrica.CrearClienteAdmin().DeleteAsync($"{Ruta}/999999");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    // ---------- Datos para el formulario de alta/edición ----------

    [Fact]
    public async Task PostGet_ComoAdmin_DevuelveGenerosYCines()
    {
        var admin = fabrica.CrearClienteAdmin();
        var generoId = await admin.CrearGeneroAsync();
        var cineId = await admin.CrearCineAsync();

        var datos = await admin.GetFromJsonAsync<JsonElement>($"{Ruta}/PostGet");

        Assert.Contains(datos.GetProperty("generos").EnumerateArray(), g => g.GetProperty("id").GetInt32() == generoId);
        Assert.Contains(datos.GetProperty("cines").EnumerateArray(), c => c.GetProperty("id").GetInt32() == cineId);
    }

    [Fact]
    public async Task PostGet_SinToken_Responde401()
    {
        var respuesta = await fabrica.CrearCliente().GetAsync($"{Ruta}/PostGet");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
